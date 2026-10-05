// ============================================================================
//  Funciones.cs  (parte de Utilidades)
//  Entrada de datos validada y menús navegables con las flechas.
//  Requisitos: .NET 6 o superior.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

using static Utilidades.DecoracionConsola;

namespace Utilidades
{
    public static class Funciones
    {
        // --------------------------------------------------------------------
        //  Entradas simples
        // --------------------------------------------------------------------

        public static bool PedirBool(string text)
        {
            while (true)
            {
                // Trim tolera espacios accidentales ("y ").
                // OrdinalIgnoreCase evita problemas de cultura de ToLower() (por ejemplo, la "I" turca).
                string respuesta = PedirString(text).Trim();

                if (respuesta.Equals("y", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (respuesta.Equals("n", StringComparison.OrdinalIgnoreCase))
                    return false;

                ImprimirMesajeDeError("opcion no valida (escriba y o n)");
            }
        }

        public static string PedirString(string text)
        {
            MyConsole.Write(text);
            return MyConsole.ReadLine();
        }

        // Espera una tecla, limpia la pantalla y borra el historial para que
        // la pantalla siguiente empiece vacía.
        public static void WaitAndClean()
        {
            // Con la entrada redirigida ReadKey lanzaría una excepción.
            // intercept = true evita que la tecla pulsada se muestre en pantalla.
            if (!Console.IsInputRedirected)
                MyConsole.ReadKey(true);

            MyConsole.Clear();
            MyConsole.ReinicializarRegistros();
        }

        // --------------------------------------------------------------------
        //  Números
        // --------------------------------------------------------------------

        public static double PedirDouble(string mensaje, double min = double.MinValue, double max = double.MaxValue)
        {
            ValidarRango(min, max);

            while (true)
            {
                string texto = PedirString(mensaje);

                // TryParse acepta "NaN" y "Infinity", y NaN pasa cualquier comparación
                // de rango (NaN < min es false): se rechazan ambos.
                if (!TryParseDouble(texto, out double valor) || !double.IsFinite(valor))
                {
                    ImprimirMesajeDeError("numero invalido");
                    continue;
                }

                if (valor < min || valor > max)
                {
                    ImprimirMesajeDeError($"numero fuera de rango (permitido: {DescribirRango(min, max)})");
                    continue;
                }

                return valor;
            }
        }

        public static int PedirInt(string mensaje, double min = int.MinValue, double max = int.MaxValue)
        {
            ValidarRango(min, max);

            // Los límites llegan como double: se llevan al rango real de int y se
            // redondean hacia adentro (0.2 → 1, 9.8 → 9). Si no queda ningún entero
            // posible, el usuario no podría responder nunca: se avisa al programador.
            double minReal = Math.Max(Math.Ceiling(min), int.MinValue);
            double maxReal = Math.Min(Math.Floor(max), int.MaxValue);

            if (minReal > maxReal)
                throw new ArgumentException($"No hay ningun entero entre {min} y {max}.");

            int minimo = (int)minReal;
            int maximo = (int)maxReal;

            while (true)
            {
                string texto = PedirString(mensaje);

                if (!int.TryParse(texto, NumberStyles.Integer, CultureInfo.CurrentCulture, out int valor))
                {
                    ImprimirMesajeDeError("numero invalido");
                    continue;
                }

                if (valor < minimo || valor > maximo)
                {
                    ImprimirMesajeDeError($"numero fuera de rango (permitido: {minimo} a {maximo})");
                    continue;
                }

                return valor;
            }
        }

        // Con min > max ningún número sería válido y el usuario quedaría atrapado:
        // es un error del programador y se avisa de inmediato.
        private static void ValidarRango(double min, double max)
        {
            if (double.IsNaN(min) || double.IsNaN(max))
                throw new ArgumentException("Los limites no pueden ser NaN.");

            if (min > max)
                throw new ArgumentException($"El minimo ({min}) no puede ser mayor que el maximo ({max}).");
        }

        // Texto del rango permitido para el mensaje de error; evita mostrar
        // los valores extremos de double cuando un límite no se indicó.
        private static string DescribirRango(double min, double max)
        {
            bool sinMinimo = min == double.MinValue;
            bool sinMaximo = max == double.MaxValue;

            if (sinMinimo && sinMaximo) return "cualquier valor";
            if (sinMinimo) return $"hasta {max}";
            if (sinMaximo) return $"desde {min}";

            return $"{min} a {max}";
        }

        // Acepta tanto el formato regional (en es-AR: 3,5) como el invariante (3.5).
        // NumberStyles.Float no admite separador de miles, así que "1.234" nunca se
        // interpreta como mil doscientos treinta y cuatro.
        private static bool TryParseDouble(string texto, out double valor)
        {
            texto = texto.Trim();

            return double.TryParse(texto, NumberStyles.Float, CultureInfo.CurrentCulture, out valor)
                || double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }

        // --------------------------------------------------------------------
        //  Contraseña
        // --------------------------------------------------------------------

        // Lee una contraseña mostrando un * por carácter. Mientras dura la lectura
        // el historial de MyConsole se desactiva para no conservar lo escrito.
        public static string LeerClave()
        {
            bool guardadoPrevio = MyConsole.GuardadoActivo;
            MyConsole.DesactivarGuardado();

            try
            {
                // Sin teclado interactivo no se puede ocultar lo escrito: se lee una línea normal.
                return Console.IsInputRedirected
                    ? MyConsole.ReadLine()
                    : LeerClaveInteractiva();
            }
            finally
            {
                if (guardadoPrevio)
                    MyConsole.ActivarGuardado();
            }
        }

        // Nombre anterior de LeerClave, conservado para no romper programas existentes.
        [Obsolete("Use LeerClave.")]
        public static string LeerNumeroClave() => LeerClave();

        private static string LeerClaveInteractiva()
        {
            // StringBuilder en lugar de "password += ..." (cada += crearía un string nuevo).
            StringBuilder password = new();

            ConsoleKeyInfo info = MyConsole.ReadKey(true);

            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.Length--;
                        BorrarUltimoAsterisco();
                    }
                }
                // Flechas, Tab, Esc, F1... producen caracteres de control: se ignoran.
                else if (!char.IsControl(info.KeyChar))
                {
                    password.Append(info.KeyChar);
                    MyConsole.Write("*");
                }

                info = MyConsole.ReadKey(true);
            }

            MyConsole.WriteLine("");

            return password.ToString();
        }

        // Borra el último asterisco de la pantalla. Si el cursor está en la columna 0
        // (la contraseña pasó a la línea siguiente), retrocede al final de la línea anterior.
        private static void BorrarUltimoAsterisco()
        {
            int x = MyConsole.CursorLeft;
            int y = MyConsole.CursorTop;

            if (x > 0)
            {
                x--;
            }
            else if (y > 0)
            {
                x = Math.Max(0, MyConsole.BufferWidth - 1);
                y--;
            }
            else
            {
                return; // ya está en el origen, no hay nada que borrar
            }

            MyConsole.SetCursorPosition(x, y);
            MyConsole.Write(" ");
            MyConsole.SetCursorPosition(x, y);
        }

        // --------------------------------------------------------------------
        //  Caracteres, fechas y horas
        // --------------------------------------------------------------------

        public static char PedirChar(string mensaje)
        {
            while (true)
            {
                string texto = PedirString(mensaje);

                if (char.TryParse(texto, out char c))
                    return c;

                ImprimirMesajeDeError("char invalido (se espera exactamente un caracter)");
            }
        }

        public static DateOnly PedirDateOnly(string mensaje)
        {
            while (true)
            {
                // Se usa la cultura actual: el usuario escribe la fecha como en su región.
                if (DateOnly.TryParse(PedirString(mensaje), out DateOnly fecha))
                    return fecha;

                ImprimirMesajeDeError("fecha invalida");
            }
        }

        public static TimeOnly PedirTimeOnly(string mensaje)
        {
            while (true)
            {
                if (TimeOnly.TryParse(PedirString(mensaje), out TimeOnly hora))
                    return hora;

                ImprimirMesajeDeError("hora invalida");
            }
        }

        public static DateTime PedirDateTime(string mensajeData, string mensajeTime)
        {
            DateOnly data = PedirDateOnly(mensajeData);
            TimeOnly time = PedirTimeOnly(mensajeTime);
            return data.ToDateTime(time);
        }

        public static TimeSpan PedirTimeSpan(string mensaje)
        {
            while (true)
            {
                // TimeSpan.TryParse("5") devuelve 5 DÍAS (no segundos ni horas).
                // Es el comportamiento de .NET; conviene pedir el formato "hh:mm:ss".
                if (TimeSpan.TryParse(PedirString(mensaje), out TimeSpan duracion))
                    return duracion;

                ImprimirMesajeDeError("duracion invalida");
            }
        }

        // --------------------------------------------------------------------
        //  Enums
        // --------------------------------------------------------------------

        // Nombres de los miembros de un enum en el orden en que se declararon.
        // (Enum.GetNames los devuelve ordenados por valor numérico.)
        private static string[] NombresEnOrdenDeclaracion<TEnum>() where TEnum : struct, Enum =>
            typeof(TEnum)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(campo => campo.Name)
                .ToArray();

        public static TEnum PedirEnum<TEnum>(string mensaje) where TEnum : struct, Enum
        {
            string[] opciones = NombresEnOrdenDeclaracion<TEnum>();

            // Un enum sin miembros produciría un menú vacío.
            if (opciones.Length == 0)
                throw new InvalidOperationException($"El enum {typeof(TEnum).Name} no tiene valores.");

            int opcion = PedirOpcionMenu(opciones, mensaje);

            return Enum.Parse<TEnum>(opciones[opcion]);
        }

        // Para enums con [Flags]: permite marcar varios valores y los combina.
        public static TEnum PedirEnumFlags<TEnum>(string mensaje) where TEnum : struct, Enum
        {
            if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
                throw new InvalidOperationException(
                    $"El enum {typeof(TEnum).Name} no tiene [Flags]; use PedirEnum.");

            string[] opciones = NombresEnOrdenDeclaracion<TEnum>();

            if (opciones.Length == 0)
                throw new InvalidOperationException($"El enum {typeof(TEnum).Name} no tiene valores.");

            List<int> elegidos = PedirOpcionMultipleMenu(opciones, mensaje);

            // Nada marcado = valor 0 (default).
            if (elegidos.Count == 0)
                return default;

            // Enum.Parse admite nombres separados por coma y los combina (OR).
            return Enum.Parse<TEnum>(string.Join(",", elegidos.Select(i => opciones[i])));
        }

        // --------------------------------------------------------------------
        //  Menús
        // --------------------------------------------------------------------

        // Menú de selección única: devuelve el índice de la opción elegida.
        public static int PedirOpcionMenu
        (
            string[] menu,
            string mensaje,
            ConsoleColor seleccion = ConsoleColor.White,
            ConsoleColor cursor = ConsoleColor.Green,
            ConsoleColor noSeleccion = ConsoleColor.DarkGreen,
            ConsoleColor textoNormal = ConsoleColor.DarkGreen
        )
        {
            return EjecutarMenu(menu, mensaje, false, null, seleccion, cursor, noSeleccion, textoNormal);
        }

        // Menú de selección múltiple: devuelve los índices marcados, de menor a mayor.
        public static List<int> PedirOpcionMultipleMenu
        (
            string[] menu,
            string mensaje,
            ConsoleColor seleccion = ConsoleColor.White,
            ConsoleColor cursor = ConsoleColor.Green,
            ConsoleColor noSeleccion = ConsoleColor.DarkGreen,
            ConsoleColor textoNormal = ConsoleColor.DarkGreen
        )
        {
            List<int> marcados = new();

            EjecutarMenu(menu, mensaje, true, marcados, seleccion, cursor, noSeleccion, textoNormal);

            marcados.Sort();

            return marcados;
        }

        // Núcleo común de los dos menús.
        //   multiple = false → devuelve el índice elegido.
        //   multiple = true  → llena "marcados" con los índices marcados (el valor devuelto se ignora).
        private static int EjecutarMenu
        (
            string[] menu,
            string mensaje,
            bool multiple,
            List<int>? marcados,
            ConsoleColor seleccion,
            ConsoleColor cursor,
            ConsoleColor noSeleccion,
            ConsoleColor textoNormal
        )
        {
            ArgumentNullException.ThrowIfNull(menu);
            if (menu.Length == 0)
                throw new ArgumentException("El menu no puede estar vacio.", nameof(menu));

            mensaje ??= "";

            // Sin consola interactiva no se pueden usar flechas ni limpiar la pantalla.
            if (!MyConsole.EsInteractiva)
                return EjecutarMenuSimple(menu, mensaje, multiple, marcados);

            int index = 0;
            int desplazamiento = 0;   // primera opción visible (para menús más altos que la ventana)
            bool ultimaIteracion = false;
            bool fin = false;
            bool exito = false;

            ConsoleColor colorOriginal = MyConsole.ForegroundColor;
            bool guardadoPrevio = MyConsole.GuardadoActivo;

            // Mientras se dibujan los cuadros intermedios no se guarda nada en el historial.
            MyConsole.DesactivarGuardado();

            // try/finally: si algo falla a mitad del menú, el color y el estado de
            // guardado no quedan alterados.
            try
            {
                do
                {
                    MyConsole.Clear();

                    // Volver a dibujar lo que había antes del menú.
                    MyConsole.ReimprimirRegistros();
                    MyConsole.SetCursorPosition(0, MyConsole.CursorTop);

                    if (ultimaIteracion)
                    {
                        fin = true;

                        // El cuadro final del menú sí queda registrado, y el guardado
                        // queda activado cuando el menú termina.
                        MyConsole.ActivarGuardado();
                    }

                    MyConsole.ForegroundColor = textoNormal;
                    MyConsole.WriteLine(mensaje);
                    ImprimirMesajeDeInformacion(multiple
                        ? "Para desplazarse: flechitas. Para seleccionar: Espacio. Para salir: Enter"
                        : "Para desplazarse: flechitas. Para seleccionar: Enter");

                    // Si el menú es más alto que lo que queda de ventana, se muestra solo
                    // una parte (con indicadores) y se desplaza junto con el cursor.
                    int filasLibres = MyConsole.AltoSeguro - MyConsole.CursorTop - 1;
                    bool conVentana = menu.Length > filasLibres;
                    int visibles = conVentana ? Math.Max(1, filasLibres - 2) : menu.Length;

                    if (index < desplazamiento)
                        desplazamiento = index;
                    else if (index >= desplazamiento + visibles)
                        desplazamiento = index - visibles + 1;

                    desplazamiento = Math.Clamp(desplazamiento, 0, menu.Length - visibles);

                    if (conVentana)
                        ImprimirIndicador(desplazamiento > 0, "arriba", noSeleccion);

                    // Cada opción se recorta a una sola línea: si ocupara dos, el menú
                    // crecería y provocaría desplazamiento de la pantalla.
                    int anchoTexto = Math.Max(1, MyConsole.AnchoSeguro - 5);

                    for (int i = desplazamiento; i < desplazamiento + visibles; i++)
                    {
                        if (multiple && marcados!.Contains(i))
                            MyConsole.ForegroundColor = seleccion;
                        else if (i == index)
                            MyConsole.ForegroundColor = (!multiple && ultimaIteracion) ? seleccion : cursor;
                        else
                            MyConsole.ForegroundColor = noSeleccion;

                        string texto = Recortar(menu[i] ?? "", anchoTexto);
                        MyConsole.WriteLine((i == index ? " > " : "   ") + texto);
                    }

                    if (conVentana)
                        ImprimirIndicador(desplazamiento + visibles < menu.Length, "abajo", noSeleccion);

                    // En el último cuadro no se espera tecla: solo se muestra el resultado.
                    if (!ultimaIteracion)
                    {
                        ConsoleKeyInfo tecla = MyConsole.ReadKey(true);

                        switch (tecla.Key)
                        {
                            case ConsoleKey.UpArrow:
                                if (index > 0) index--;
                                break;

                            case ConsoleKey.DownArrow:
                                if (index < menu.Length - 1) index++;
                                break;

                            case ConsoleKey.Home:
                                index = 0;
                                break;

                            case ConsoleKey.End:
                                index = menu.Length - 1;
                                break;

                            case ConsoleKey.PageUp:
                                index = Math.Max(0, index - visibles);
                                break;

                            case ConsoleKey.PageDown:
                                index = Math.Min(menu.Length - 1, index + visibles);
                                break;

                            case ConsoleKey.Spacebar when multiple:
                                // Remove devuelve false si el índice no estaba marcado: entonces se marca.
                                if (!marcados!.Remove(index))
                                    marcados.Add(index);
                                break;

                            case ConsoleKey.Enter:
                                ultimaIteracion = true;
                                break;
                        }
                    }
                } while (!fin);

                exito = true;
            }
            finally
            {
                MyConsole.ForegroundColor = colorOriginal;

                // Si hubo una excepción, el guardado vuelve al estado que tenía antes del menú.
                if (!exito)
                {
                    if (guardadoPrevio) MyConsole.ActivarGuardado();
                    else MyConsole.DesactivarGuardado();
                }
            }

            return index;
        }

        // Versión sin teclas, para entrada o salida redirigidas: se muestra la lista
        // numerada y se pide el número (o números separados por coma en el menú múltiple).
        private static int EjecutarMenuSimple(string[] menu, string mensaje, bool multiple, List<int>? marcados)
        {
            MyConsole.WriteLine(mensaje);

            for (int i = 0; i < menu.Length; i++)
                MyConsole.WriteLine($"  {i + 1}) {menu[i]}");

            if (!multiple)
                return PedirInt("Opcion (numero): ", 1, menu.Length) - 1;

            while (true)
            {
                string entrada = PedirString("Opciones (numeros separados por coma, vacio = ninguna): ");

                string[] partes = entrada.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                List<int> elegidos = new();
                bool valido = true;

                foreach (string parte in partes)
                {
                    if (int.TryParse(parte, out int numero) && numero >= 1 && numero <= menu.Length)
                    {
                        if (!elegidos.Contains(numero - 1))
                            elegidos.Add(numero - 1);
                    }
                    else
                    {
                        valido = false;
                        break;
                    }
                }

                if (!valido)
                {
                    ImprimirMesajeDeError("opcion no valida");
                    continue;
                }

                marcados!.AddRange(elegidos);
                return 0;
            }
        }

        // Línea de aviso "... mas opciones arriba/abajo" (o línea vacía si no hay más).
        private static void ImprimirIndicador(bool hayMas, string direccion, ConsoleColor color)
        {
            MyConsole.ForegroundColor = color;
            MyConsole.WriteLine(hayMas ? $"   ... mas opciones {direccion}" : "");
        }

        // Deja el texto en una sola línea y lo recorta con "..." si supera el ancho.
        private static string Recortar(string texto, int ancho)
        {
            // Saltos de línea o tabulaciones dentro de una opción romperían el dibujo del menú.
            texto = texto.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

            if (texto.Length <= ancho)
                return texto;

            if (ancho <= 3)
                return texto.Substring(0, ancho);

            return texto.Substring(0, ancho - 3) + "...";
        }
    }
}
