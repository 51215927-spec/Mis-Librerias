// ============================================================================
//  MyConsole.cs  (parte de Utilidades)
//  Envoltorio de Console que puede registrar lo escrito para volver a dibujarlo
//  después de limpiar la pantalla. Requisitos: .NET 6 o superior.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace Utilidades
{
    public static class MyConsole
    {
        // Un fragmento de texto escrito, con su color y la posición donde empezó.
        private readonly struct Linea
        {
            public string Texto { get; }
            public ConsoleColor ForegroundColor { get; }
            public ConsoleColor BackgroundColor { get; }
            public int X { get; }
            public int Y { get; }

            public Linea
            (
                string texto,
                ConsoleColor foregroundColor,
                ConsoleColor backgroundColor,
                int x,
                int y
            )
            {
                Texto = texto;
                ForegroundColor = foregroundColor;
                BackgroundColor = backgroundColor;
                X = x;
                Y = y;
            }
        }

        private static readonly List<Linea> registros = new();

        // Protege la lista de registros cuando varios hilos escriben a la vez.
        private static readonly object bloqueo = new();

        // Indica si Write/WriteLine/ReadLine registran lo que se escribe.
        // Empieza activado para que todo lo escrito antes del primer menú también se conserve.
        private static volatile bool guardarDatos = true;

        // Tope de memoria: al superarlo se descartan los registros más antiguos.
        private const int MaximoRegistros = 5000;

        // Errores típicos cuando no hay una consola real (salida redirigida,
        // ventana minimizada, plataformas sin soporte) o cuando una posición queda fuera del buffer.
        private static bool EsErrorDeConsola(Exception ex) =>
            ex is IOException
                or PlatformNotSupportedException
                or InvalidOperationException
                or ArgumentOutOfRangeException;

        // Igual que EsErrorDeConsola, pero sin ArgumentOutOfRangeException:
        // una coordenada inválida pasada por el programador debe seguir lanzando error.
        private static bool EsErrorDeEntorno(Exception ex) =>
            ex is IOException
                or PlatformNotSupportedException
                or InvalidOperationException;

        // ---------------------------------------------------------------- Colores

        public static ConsoleColor ForegroundColor
        {
            get => Console.ForegroundColor;
            set => Console.ForegroundColor = value;
        }

        public static ConsoleColor BackgroundColor
        {
            get => Console.BackgroundColor;
            set => Console.BackgroundColor = value;
        }

        public static void ResetColor() { Console.ResetColor(); }

        // ----------------------------------------------------------------- Cursor

        // Lee la posición del cursor; si no hay consola real devuelve (0, 0).
        private static (int X, int Y) LeerPosicion()
        {
            try
            {
                return (Console.CursorLeft, Console.CursorTop);
            }
            catch (Exception ex) when (EsErrorDeConsola(ex))
            {
                return (0, 0);
            }
        }

        public static int CursorLeft { get => LeerPosicion().X; }
        public static int CursorTop { get => LeerPosicion().Y; }

        public static bool CursorVisible
        {
            // En algunas plataformas leer la visibilidad no está soportado.
            get
            {
                try { return Console.CursorVisible; }
                catch (Exception ex) when (EsErrorDeConsola(ex)) { return true; }
            }
            set
            {
                try { Console.CursorVisible = value; }
                catch (Exception ex) when (EsErrorDeConsola(ex)) { }
            }
        }

        public static void SetCursorPosition(int x, int y)
        {
            try
            {
                Console.SetCursorPosition(x, y);
            }
            catch (Exception ex) when (EsErrorDeEntorno(ex))
            {
                // Sin consola real no hay cursor que mover.
            }
        }

        // ------------------------------------------------------ Tamaño de consola

        // Ancho y alto de la ventana; si no se pueden leer (o dan 0) se usa 80 x 24.
        public static int AnchoSeguro
        {
            get
            {
                try { int ancho = Console.WindowWidth; return ancho > 0 ? ancho : 80; }
                catch (Exception ex) when (EsErrorDeConsola(ex)) { return 80; }
            }
        }

        public static int AltoSeguro
        {
            get
            {
                try { int alto = Console.WindowHeight; return alto > 0 ? alto : 24; }
                catch (Exception ex) when (EsErrorDeConsola(ex)) { return 24; }
            }
        }

        public static int WindowWidth { get => AnchoSeguro; }
        public static int WindowHeight { get => AltoSeguro; }

        // Tamaño del buffer; si no se puede leer se usa el tamaño de la ventana.
        public static int BufferWidth
        {
            get
            {
                try { int ancho = Console.BufferWidth; return ancho > 0 ? ancho : AnchoSeguro; }
                catch (Exception ex) when (EsErrorDeConsola(ex)) { return AnchoSeguro; }
            }
        }

        public static int BufferHeight
        {
            get
            {
                try { int alto = Console.BufferHeight; return alto > 0 ? alto : AltoSeguro; }
                catch (Exception ex) when (EsErrorDeConsola(ex)) { return AltoSeguro; }
            }
        }

        // true si hay teclado y pantalla reales (ni la entrada ni la salida están redirigidas).
        public static bool EsInteractiva =>
            !Console.IsInputRedirected && !Console.IsOutputRedirected;

        // ---------------------------------------------------------------- Pantalla

        public static void Clear()
        {
            try
            {
                Console.Clear();
            }
            catch (Exception ex) when (EsErrorDeConsola(ex))
            {
                // Salida redirigida: no hay pantalla que limpiar.
            }
        }

        // ------------------------------------------------- Guardado y registros

        public static void ActivarGuardado() { guardarDatos = true; }
        public static void DesactivarGuardado() { guardarDatos = false; }
        public static bool GuardadoActivo { get => guardarDatos; }

        public static void ReinicializarRegistros()
        {
            lock (bloqueo)
                registros.Clear();
        }

        private static void Registrar(string texto, ConsoleColor foreground, ConsoleColor background, int x, int y)
        {
            lock (bloqueo)
            {
                registros.Add(new Linea(texto, foreground, background, x, y));

                if (registros.Count > MaximoRegistros)
                    registros.RemoveRange(0, registros.Count - MaximoRegistros);
            }
        }

        // ---------------------------------------------------------------Escritura

        public static void Write(string? mensaje)
        {
            mensaje ??= "";

            (int x, int y) = LeerPosicion();

            ConsoleColor foreground = Console.ForegroundColor;
            ConsoleColor background = Console.BackgroundColor;

            Console.Write(mensaje);

            if (guardarDatos)
                Registrar(mensaje, foreground, background, x, y);
        }

        public static void WriteLine(string? mensaje)
        {
            mensaje ??= "";

            (int x, int y) = LeerPosicion();

            ConsoleColor foreground = Console.ForegroundColor;
            ConsoleColor background = Console.BackgroundColor;

            Console.WriteLine(mensaje);

            if (guardarDatos)
                Registrar(mensaje + Environment.NewLine, foreground, background, x, y);
        }

        // Vuelve a dibujar todo lo registrado, cada texto en su posición y color originales.
        public static void ReimprimirRegistros()
        {
            // Con la salida redirigida no hay pantalla donde reposicionar el cursor.
            if (Console.IsOutputRedirected)
                return;

            // Se recorre una copia para que otro hilo pueda seguir escribiendo.
            Linea[] copia;
            lock (bloqueo)
                copia = registros.ToArray();

            int anchoBuffer = BufferWidth;
            int altoBuffer = BufferHeight;

            // Se guardan los colores actuales para restaurarlos al terminar
            // (ResetColor borraría los colores personalizados del programa).
            ConsoleColor foregroundOriginal = Console.ForegroundColor;
            ConsoleColor backgroundOriginal = Console.BackgroundColor;

            try
            {
                foreach (Linea linea in copia)
                {
                    // Se omiten los textos cuya posición ya no existe en el buffer.
                    if (linea.X < 0 ||
                        linea.Y < 0 ||
                        linea.X >= anchoBuffer ||
                        linea.Y >= altoBuffer)
                        continue;

                    // Si la ventana se achicó, la posición puede ser inválida aunque pase
                    // la comprobación anterior: se omite ese texto y se sigue con el resto.
                    try
                    {
                        Console.SetCursorPosition(linea.X, linea.Y);
                    }
                    catch (Exception ex) when (EsErrorDeConsola(ex))
                    {
                        continue;
                    }

                    Console.ForegroundColor = linea.ForegroundColor;
                    Console.BackgroundColor = linea.BackgroundColor;

                    Console.Write(linea.Texto);
                }
            }
            finally
            {
                Console.ForegroundColor = foregroundOriginal;
                Console.BackgroundColor = backgroundOriginal;
            }
        }

        // ----------------------------------------------------------------Lectura

        public static string ReadLine()
        {
            (int x, int y) = LeerPosicion();

            ConsoleColor foreground = Console.ForegroundColor;
            ConsoleColor background = Console.BackgroundColor;

            string? texto = Console.ReadLine();

            // Console.ReadLine devuelve null cuando la entrada se cerró (fin de archivo,
            // Ctrl+Z / Ctrl+D). Se lanza una excepción para que los métodos Pedir*
            // no queden en un bucle infinito pidiendo el dato.
            if (texto is null)
                throw new EndOfStreamException("Se cerro la entrada de datos (fin de archivo).");

            if (guardarDatos)
                Registrar(texto + Environment.NewLine, foreground, background, x, y);

            return texto;
        }

        public static ConsoleKeyInfo ReadKey(bool intercept = false) { return Console.ReadKey(intercept); }
    }
}
