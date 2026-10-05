// ============================================================================
//  DecoracionConsola.cs  (parte de Utilidades)
//  Mensajes de colores, barras de progreso, marcos, listas y tablas.
//  Requisitos: .NET 6 o superior.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Utilidades
{
    public static class DecoracionConsola
    {
        // --------------------------------------------------------------------
        //  Progreso
        // --------------------------------------------------------------------

        public static void MostrarProgreso(int time)
        {
            // Los cuadros intermedios no se registran: serían 101 entradas por cada
            // llamada y llenarían el historial. Solo queda registrado el resultado final.
            EjecutarSinRegistrar(() =>
            {
                for (int i = 0; i <= 100; i++)
                {
                    MyConsole.Write($"\rProgreso: {i}%");
                    Thread.Sleep(Math.Max(1, time / 101));
                }
            });

            MyConsole.Write("\rProgreso: 100%");
            MyConsole.WriteLine("");
        }

        public static void MostrarBarraDeProgreso(int time)
        {
            // La barra se adapta al ancho de la ventana. El texto "[" + barra + "] 100%"
            // ocupa (ancho + 7) caracteres; se deja 1 de margen para que no salte de línea.
            int ancho = Math.Clamp(MyConsole.AnchoSeguro - 8, 10, 100);

            string Dibujar(int porcentaje)
            {
                int llenos = porcentaje * ancho / 100;
                string barra = new string('#', llenos) + new string('-', ancho - llenos);
                return $"\r[{barra}] {porcentaje}%";
            }

            // Los cuadros intermedios no se registran (ver MostrarProgreso).
            EjecutarSinRegistrar(() =>
            {
                for (int i = 0; i <= 100; i++)
                {
                    MyConsole.Write(Dibujar(i));
                    Thread.Sleep(Math.Max(1, time / 100));
                }
            });

            MyConsole.Write(Dibujar(100));
            MyConsole.WriteLine("");
        }

        // Ejecuta una acción con el guardado de MyConsole desactivado y luego
        // restaura el estado que tenía, aunque la acción lance una excepción.
        private static void EjecutarSinRegistrar(Action accion)
        {
            bool guardadoPrevio = MyConsole.GuardadoActivo;
            MyConsole.DesactivarGuardado();

            try
            {
                accion();
            }
            finally
            {
                if (guardadoPrevio)
                    MyConsole.ActivarGuardado();
            }
        }

        // --------------------------------------------------------------------
        //  Separadores y marcos
        // --------------------------------------------------------------------

        public static void ImprimirSeparador(char caracter = '═')
        {
            // Se usa ancho - 1: escribir exactamente el ancho completo y luego un salto
            // de línea dejaría una línea en blanco de más.
            int largo = Math.Max(1, MyConsole.AnchoSeguro - 1);
            MyConsole.WriteLine(new string(caracter, largo));
        }

        public static void ImprimirTextoConMarco(string texto)
        {
            // Se normaliza el texto (null, saltos de línea, tabulaciones) y se parte en
            // líneas que quepan en la ventana, para que el marco no se desalinee.
            texto = NormalizarTexto(texto);

            // El marco agrega 6 caracteres ("║  " y "  ║"); se deja 1 más de margen.
            int anchoMaximo = Math.Max(1, MyConsole.AnchoSeguro - 1 - 6);

            string[] lineas = texto
                .Split('\n')
                .SelectMany(linea => PartirLinea(linea, anchoMaximo))
                .ToArray();

            int ancho = lineas.Max(linea => linea.Length);

            MyConsole.WriteLine("╔" + new string('═', ancho + 4) + "╗");

            foreach (string linea in lineas)
                MyConsole.WriteLine("║  " + linea.PadRight(ancho) + "  ║");

            MyConsole.WriteLine("╚" + new string('═', ancho + 4) + "╝");
        }

        // --------------------------------------------------------------------
        //  Mensajes de colores
        // --------------------------------------------------------------------

        public static void ImprimirMesajeDeExito(string mensaje) =>
            ImprimirConColor(ConsoleColor.Green, "EXITO", mensaje);

        public static void ImprimirMesajeDeError(string mensaje) =>
            ImprimirConColor(ConsoleColor.Red, "ERROR", mensaje);

        public static void ImprimirMesajeDeAdvertencia(string mensaje) =>
            ImprimirConColor(ConsoleColor.Yellow, "ADVERTENCIA", mensaje);

        public static void ImprimirMesajeDeInformacion(string mensaje) =>
            ImprimirConColor(ConsoleColor.Cyan, "INFO", mensaje);

        // Imprime "ETIQUETA: mensaje" en el color indicado y restaura el color original
        // aunque ocurra un error al escribir.
        private static void ImprimirConColor(ConsoleColor color, string etiqueta, string mensaje)
        {
            ConsoleColor original = MyConsole.ForegroundColor;

            try
            {
                MyConsole.ForegroundColor = color;
                MyConsole.WriteLine($"{etiqueta}: {mensaje}"); // un mensaje null se imprime vacío
            }
            finally
            {
                MyConsole.ForegroundColor = original;
            }
        }

        // --------------------------------------------------------------------
        //  Listas y tablas
        // --------------------------------------------------------------------

        public static void SaltarLineas(int cantidad = 1)
        {
            for (int i = 0; i < cantidad; i++)
                MyConsole.WriteLine("");
        }

        public static void MostrarLista(string[] elementos)
        {
            ArgumentNullException.ThrowIfNull(elementos);

            foreach (string elemento in elementos)
                MyConsole.WriteLine($"  • {elemento}"); // un elemento null se imprime vacío
        }

        public static void MostrarTabla(string[,] matriz)
        {
            ArgumentNullException.ThrowIfNull(matriz);

            int filas = matriz.GetLength(0);
            int columnas = matriz.GetLength(1);

            // Sin filas o sin columnas no hay nada que dibujar.
            if (filas == 0 || columnas == 0)
                return;

            // 1) Normalizar cada celda (null → vacío, saltos de línea y tabulaciones),
            //    partirla en líneas y calcular el ancho de cada columna.
            string[,][] celdas = new string[filas, columnas][];
            int[] anchos = new int[columnas];

            for (int i = 0; i < filas; i++)
            {
                for (int j = 0; j < columnas; j++)
                {
                    celdas[i, j] = NormalizarTexto(matriz[i, j]).Split('\n');

                    foreach (string linea in celdas[i, j])
                    {
                        if (linea.Length > anchos[j])
                            anchos[j] = linea.Length;
                    }
                }
            }

            // 2) Si la tabla es más ancha que la ventana, se reduce la columna más ancha,
            //    de a un carácter, hasta que entre (o hasta que ya no se pueda).
            //    Cada columna agrega 1 borde "|" y 2 espacios de margen; hay 1 borde final.
            int disponible = MyConsole.AnchoSeguro - 1 - (columnas + 1) - (2 * columnas);
            int total = anchos.Sum();

            while (total > disponible)
            {
                int masAncha = Array.IndexOf(anchos, anchos.Max());

                if (anchos[masAncha] <= 1)
                    break;

                anchos[masAncha]--;
                total--;
            }

            // 3) Partir las líneas que no caben en el ancho de su columna.
            for (int i = 0; i < filas; i++)
            {
                for (int j = 0; j < columnas; j++)
                {
                    int anchoColumna = Math.Max(1, anchos[j]);

                    celdas[i, j] = celdas[i, j]
                        .SelectMany(linea => PartirLinea(linea, anchoColumna))
                        .ToArray();
                }
            }

            // Dibuja una línea horizontal "+-----+-----+".
            void Separador()
            {
                MyConsole.Write("+");

                for (int j = 0; j < columnas; j++)
                {
                    MyConsole.Write(new string('-', Math.Max(1, anchos[j]) + 2));
                    MyConsole.Write("+");
                }

                MyConsole.WriteLine("");
            }

            // 4) Dibujar fila por fila; el alto de una fila es el de su celda con más líneas.
            Separador();

            for (int i = 0; i < filas; i++)
            {
                int altoFila = 1;

                for (int j = 0; j < columnas; j++)
                    altoFila = Math.Max(altoFila, celdas[i, j].Length);

                for (int linea = 0; linea < altoFila; linea++)
                {
                    MyConsole.Write("|");

                    for (int j = 0; j < columnas; j++)
                    {
                        string texto = linea < celdas[i, j].Length ? celdas[i, j][linea] : "";

                        MyConsole.Write(" " + texto.PadRight(Math.Max(1, anchos[j])) + " |");
                    }

                    MyConsole.WriteLine("");
                }

                Separador();
            }
        }

        // --------------------------------------------------------------------
        //  Auxiliares privados
        // --------------------------------------------------------------------

        // null → ""; unifica los saltos de línea en '\n'; reemplaza las tabulaciones por
        // espacios (una tabulación ocupa un ancho variable y desalinea los marcos).
        private static string NormalizarTexto(string? texto)
        {
            return (texto ?? "")
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace("\t", "    ");
        }

        // Parte una línea en trozos de como máximo "ancho" caracteres.
        // Una línea vacía devuelve una línea vacía, para no perder renglones en blanco.
        private static IEnumerable<string> PartirLinea(string linea, int ancho)
        {
            if (linea.Length == 0)
            {
                yield return "";
                yield break;
            }

            for (int i = 0; i < linea.Length; i += ancho)
                yield return linea.Substring(i, Math.Min(ancho, linea.Length - i));
        }
    }
}
