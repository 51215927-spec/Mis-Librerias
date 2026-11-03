// ============================================================================
//  CsvManager.cs  (parte de DataBases)
//  Guarda y lee tablas en archivos CSV. Los archivos, columnas y miembros salen del
//  EntidadModelo (configurado con ConstructorModelo); ya no se usan atributos.
//  Requisitos: .NET 8 o superior (DataBases usa miembros abstractos estáticos en interfaces; ver ConstructorModelo.cs).
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DataBases
{
    // ───────────────────────── Excepciones propias ─────────────────────────

    /// <summary>Base de todas las excepciones de CsvManager.</summary>
    public class CsvException : Exception
    {
        public CsvException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
    }

    /// <summary>El CSV (datos externos) tiene un problema. Incluye línea y columna.</summary>
    public class CsvFormatException : CsvException
    {
        public int Linea { get; }
        public string? Columna { get; }

        public CsvFormatException(string mensaje, int linea, string? columna = null, Exception? inner = null)
            : base($"Línea {linea}{(columna != null ? $", columna '{columna}'" : "")}: {mensaje}", inner)
        {
            Linea = linea;
            Columna = columna;
        }
    }

    /// <summary>El archivo fue modificado por otro proceso (o a mano) después de cargar la tabla.
    /// Guardar lo sobrescribiría y se perderían esos cambios.</summary>
    public class CsvConcurrenciaException : CsvException
    {
        public CsvConcurrenciaException(string mensaje) : base(mensaje) { }
    }

    /// <summary>La clase T no cumple las reglas del sistema (error del programador, no del CSV).</summary>
    public class CsvConfiguracionException : CsvException
    {
        public CsvConfiguracionException(string mensaje) : base(mensaje) { }
    }

    // ───────────────────────────── CsvManager ──────────────────────────────

    internal static class CsvManager
    {
        // Los nombres de archivo distinguen mayúsculas en Linux y no en Windows.
        internal static StringComparer ComparadorRutas { get; } =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        // Tiempo máximo de espera de un bloqueo que tiene otro proceso.
        private static readonly TimeSpan EsperaMaximaBloqueo = TimeSpan.FromSeconds(10);

        // Un bloqueo por archivo. Monitor protege entre hilos del mismo proceso y el archivo
        // ".lock" (abierto sin compartir) protege entre procesos.
        private sealed class EstadoBloqueo
        {
            public int Cuenta;          // veces que el hilo dueño lo tomó (se admite reentrada)
            public FileStream? Archivo; // archivo ".lock" abierto mientras Cuenta > 0
        }

        private static readonly ConcurrentDictionary<string, EstadoBloqueo> bloqueos = new(ComparadorRutas);

        // Texto con el que se escribe un valor null. Solo vale como null si va sin comillas:
        // un string cuyo contenido es "null" se escribe entre comillas y no se confunde.
        private const string NullTexto = "null";

        // Una celda leída: el texto y si venía entre comillas.
        // Permite distinguir "" (texto vacío) de una celda vacía sin comillas (null).
        private readonly record struct Celda(string Texto, bool EntreComillas);

        // ───────────────────────── BLOQUEO ENTRE PROCESOS ─────────────────────────

        /// <summary>
        /// Toma el bloqueo de uno o más archivos (rutas absolutas). Las rutas se toman siempre en el
        /// mismo orden para evitar bloqueos mutuos. Admite reentrada dentro del mismo hilo.
        /// Hay que liberarlo (Dispose) desde el mismo hilo que lo tomó.
        /// </summary>
        internal static IDisposable Bloquear(IEnumerable<string> rutasCompletas)
        {
            string[] rutas = rutasCompletas
                .Distinct(ComparadorRutas)
                .OrderBy(r => r, ComparadorRutas)
                .ToArray();

            List<EstadoBloqueo> tomados = new();

            try
            {
                foreach (string ruta in rutas)
                {
                    EstadoBloqueo estado = bloqueos.GetOrAdd(ruta, _ => new EstadoBloqueo());
                    TomarBloqueo(ruta, estado);
                    tomados.Add(estado);
                }
            }
            catch
            {
                SoltarBloqueos(tomados);
                throw;
            }

            return new LiberadorBloqueos(tomados);
        }

        private static void TomarBloqueo(string rutaCompleta, EstadoBloqueo estado)
        {
            Monitor.Enter(estado);

            try
            {
                if (estado.Cuenta == 0)
                    estado.Archivo = AbrirArchivoBloqueo(rutaCompleta);

                estado.Cuenta++;
            }
            catch
            {
                Monitor.Exit(estado);
                throw;
            }
        }

        private static void SoltarBloqueos(List<EstadoBloqueo> tomados)
        {
            for (int i = tomados.Count - 1; i >= 0; i--)
            {
                EstadoBloqueo estado = tomados[i];

                try
                {
                    estado.Cuenta--;

                    if (estado.Cuenta == 0)
                    {
                        estado.Archivo?.Dispose();
                        estado.Archivo = null;
                    }
                }
                finally
                {
                    Monitor.Exit(estado);
                }
            }
        }

        private sealed class LiberadorBloqueos : IDisposable
        {
            private List<EstadoBloqueo>? tomados;

            public LiberadorBloqueos(List<EstadoBloqueo> tomados) => this.tomados = tomados;

            public void Dispose()
            {
                List<EstadoBloqueo>? lista = tomados;
                tomados = null;

                if (lista != null)
                    SoltarBloqueos(lista);
            }
        }

        // Abre "<archivo>.lock" sin compartirlo: mientras un proceso lo tiene abierto, los demás esperan.
        // Si el proceso termina, el sistema operativo lo libera.
        private static FileStream AbrirArchivoBloqueo(string rutaCompleta)
        {
            string? directorio = Path.GetDirectoryName(rutaCompleta);

            if (!string.IsNullOrEmpty(directorio))
                Directory.CreateDirectory(directorio);

            string rutaBloqueo = rutaCompleta + ".lock";
            DateTime limite = DateTime.UtcNow + EsperaMaximaBloqueo;

            while (true)
            {
                try
                {
                    return new FileStream(rutaBloqueo, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (DateTime.UtcNow < limite)
                {
                    Thread.Sleep(25);
                }
            }
        }

        // Versión del archivo en disco: permite detectar que otro proceso lo cambió.
        // null = el archivo no existe.
        private static VersionArchivo? ObtenerVersion(string rutaCompleta)
        {
            FileInfo info = new(rutaCompleta);
            return info.Exists ? new VersionArchivo(info.LastWriteTimeUtc.Ticks, info.Length) : null;
        }

        // Mueve el archivo temporal sobre el definitivo. En Windows falla con IOException si otro
        // programa lo tiene abierto en ese instante, por eso se reintenta unas pocas veces.
        private static void MoverConReintentos(string origen, string destino)
        {
            const int intentos = 5;

            for (int i = 1; ; i++)
            {
                try
                {
                    File.Move(origen, destino, overwrite: true);
                    return;
                }
                catch (IOException) when (i < intentos)
                {
                    Thread.Sleep(50 * i);
                }
            }
        }

        // ───────────────────────────── ESCRITURA ─────────────────────────────

        internal static void Escribir<T>(EntidadModelo entidad, IEnumerable<T> datos)
        {
            ArgumentNullException.ThrowIfNull(datos);

            string rutaCompleta = entidad.Ruta;   // ya es absoluta: se resuelve una vez en Archivo(...)
            IReadOnlyList<ColumnaModelo> columnas = entidad.Columnas;

            List<string> lineas = new();

            lineas.Add(string.Join(",", columnas.Select(c => c.Nombre)));

            foreach (T dato in datos)
            {
                if (dato is null)
                    throw new ArgumentException("La tabla contiene un elemento null.", nameof(datos));

                lineas.Add(string.Join(",",
                    columnas.Select(c => FormatearValor(c.Miembro.Leer(dato), c.Miembro.Tipo))));
            }

            using (Bloquear(new[] { rutaCompleta }))
            {
                // Si otro proceso (o una edición a mano) cambió el archivo después de cargarlo,
                // guardar lo pisaría y esos cambios se perderían.
                if (!Equals(ObtenerVersion(rutaCompleta), entidad.Version))
                    throw new CsvConcurrenciaException(
                        $"El archivo {rutaCompleta} fue modificado después de cargar {entidad.Tipo.Name}. " +
                        "Guardar descartaría esos cambios. Vuelva a cargar la tabla (Cargar) y repita los cambios.");

                string? directorio = Path.GetDirectoryName(rutaCompleta);

                if (!string.IsNullOrEmpty(directorio))
                    Directory.CreateDirectory(directorio);

                // Escritura atómica: se escribe en un archivo temporal propio (nombre único) y luego se
                // reemplaza el original, de modo que un corte a mitad no daña el archivo existente.
                string temporal = $"{rutaCompleta}.{Guid.NewGuid():N}.tmp";

                try
                {
                    using (FileStream archivo = new(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (StreamWriter escritor = new(archivo, new UTF8Encoding(false)))
                    {
                        foreach (string linea in lineas)
                            escritor.WriteLine(linea);

                        escritor.Flush();
                        archivo.Flush(true);   // fuerza la escritura física antes del reemplazo
                    }

                    MoverConReintentos(temporal, rutaCompleta);
                }
                catch
                {
                    try { if (File.Exists(temporal)) File.Delete(temporal); } catch { /* ignorar */ }
                    throw;
                }

                entidad.Version = ObtenerVersion(rutaCompleta);
            }
        }

        // ────────────────────────────── LECTURA ──────────────────────────────

        /// <summary>Lee el CSV de la entidad. Si no existe: lista vacía, o FileNotFoundException con exigirArchivo.</summary>
        internal static List<T> Leer<T>(EntidadModelo entidad, bool exigirArchivo)
        {
            string rutaCompleta = entidad.Ruta;   // ya es absoluta: se resuelve una vez en Archivo(...)
            string[]? lineas;
            VersionArchivo? version;

            using (Bloquear(new[] { rutaCompleta }))
            {
                // La versión se toma antes de leer: si el archivo cambiara durante la lectura,
                // la diferencia se detecta al guardar.
                version = ObtenerVersion(rutaCompleta);
                lineas = LeerLineas(rutaCompleta);
            }

            if (lineas == null)
            {
                if (exigirArchivo)
                    throw new FileNotFoundException($"El archivo CSV no existe: {rutaCompleta}", rutaCompleta);

                entidad.Version = version;
                return new List<T>();
            }

            List<T> datos = ParsearTabla<T>(entidad, lineas);
            entidad.Version = version;
            return datos;
        }

        // Lee todas las líneas del archivo permitiendo que otros programas lo abran, lo modifiquen o lo
        // reemplacen mientras tanto. Devuelve null si el archivo no existe (la comprobación y la lectura
        // son una sola operación, sin File.Exists previo).
        private static string[]? LeerLineas(string rutaCompleta)
        {
            try
            {
                using FileStream archivo = new(
                    rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader lector = new(archivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                List<string> lineas = new();
                string? linea;

                while ((linea = lector.ReadLine()) != null)
                    lineas.Add(linea);

                return lineas.ToArray();
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        // Convierte las líneas del CSV en objetos (encabezado, validaciones y filas).
        private static List<T> ParsearTabla<T>(EntidadModelo entidad, string[] lineas)
        {
            IReadOnlyList<ColumnaModelo> columnas = entidad.Columnas;

            // Archivo vacío o con solo líneas en blanco → lista vacía.
            int indiceEncabezado = Array.FindIndex(lineas, l => !string.IsNullOrWhiteSpace(l));
            if (indiceEncabezado < 0)
            {
                return new List<T>();
            }

            // ── Encabezado ──
            int numeroEncabezado = indiceEncabezado + 1;
            List<Celda> celdasEncabezado = ParsearLinea(lineas[indiceEncabezado], numeroEncabezado);
            string[] nombres = celdasEncabezado.Select(c => c.Texto.Trim()).ToArray();

            // Excel en configuración regional argentina suele guardar con ';'.
            if (nombres.Length == 1 && nombres[0].Contains(';'))
                throw new CsvFormatException(
                    "Parece que el archivo usa ';' como separador. Este formato requiere ','.",
                    numeroEncabezado);

            string[] duplicadas = nombres.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            if (duplicadas.Length > 0)
                throw new CsvFormatException(
                    $"Columnas duplicadas en el encabezado: {string.Join(", ", duplicadas)}.",
                    numeroEncabezado);

            // Columnas del archivo que la clase no define.
            string[] desconocidas = nombres.Where(n => !columnas.Any(c => c.Nombre == n)).ToArray();
            if (desconocidas.Length > 0)
                throw new CsvFormatException(
                    $"Las columnas [{string.Join(", ", desconocidas)}] no existen en {typeof(T).Name}.",
                    numeroEncabezado);

            // Columnas de la clase que faltan en el archivo.
            string[] faltantes = columnas.Select(c => c.Nombre).Where(n => !nombres.Contains(n)).ToArray();
            if (faltantes.Length > 0)
                throw new CsvFormatException(
                    $"Faltan las columnas [{string.Join(", ", faltantes)}] requeridas por {typeof(T).Name}.",
                    numeroEncabezado);

            // Campo correspondiente a cada columna del archivo, en el orden del archivo.
            Miembro[] miembrosOrdenados = nombres
                .Select(n => columnas.First(c => c.Nombre == n).Miembro)
                .ToArray();

            // El constructor se busca una sola vez, no por cada fila.
            (ConstructorInfo constructor, int[] indices) = BuscarConstructor(typeof(T), miembrosOrdenados);
            ParameterInfo[] parametros = constructor.GetParameters();

            // ── Filas ──
            List<T> datos = new();

            for (int i = indiceEncabezado + 1; i < lineas.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lineas[i]))
                    continue;

                int numeroLinea = i + 1;
                List<Celda> celdas = ParsearLinea(lineas[i], numeroLinea);

                if (celdas.Count != nombres.Length)
                    throw new CsvFormatException(
                        $"Se esperaban {nombres.Length} columnas pero hay {celdas.Count}.",
                        numeroLinea);

                object?[] argumentos = new object?[parametros.Length];

                for (int p = 0; p < parametros.Length; p++)
                {
                    int col = indices[p];
                    argumentos[p] = ConvertirValor(
                        celdas[col],
                        parametros[p].ParameterType,
                        numeroLinea,
                        nombres[col]);
                }

                try
                {
                    datos.Add((T)constructor.Invoke(argumentos));
                }
                catch (TargetInvocationException ex)
                {
                    // El constructor lanzó una excepción (por ejemplo, una validación):
                    // el mensaje útil está en InnerException.
                    Exception causa = ex.InnerException ?? ex;
                    throw new CsvFormatException(
                        $"El constructor de {typeof(T).Name} rechazó los datos: {causa.Message}",
                        numeroLinea,
                        null,
                        causa);
                }
            }

            return datos;
        }

        // ──────────────────────────── REFLECTION ─────────────────────────────

        // true si el tipo admite null: una clase o un Nullable<T>.
        private static bool AceptaNull(Type tipo) =>
            !tipo.IsValueType || Nullable.GetUnderlyingType(tipo) != null;

        // Tipos que CsvManager sabe escribir y leer de vuelta.
        internal static bool EsTipoSoportado(Type tipo)
        {
            Type t = Nullable.GetUnderlyingType(tipo) ?? tipo;

            return t.IsEnum
                || t == typeof(string)
                || t == typeof(decimal)
                || t == typeof(DateTime)
                || t == typeof(DateTimeOffset)
                || t == typeof(DateOnly)
                || t == typeof(TimeOnly)
                || t == typeof(TimeSpan)
                || t == typeof(Guid)
                || (t.IsPrimitive && t != typeof(IntPtr) && t != typeof(UIntPtr));
        }

        internal static void ValidarConstructor(EntidadModelo entidad) =>
            BuscarConstructor(entidad.Tipo, entidad.Columnas.Select(c => c.Miembro).ToArray());

        /// <summary>
        /// Busca un constructor (público o no) cuyos parámetros coincidan con los miembros
        /// por nombre (sin distinguir mayúsculas) y tipo. Devuelve también, para cada
        /// parámetro, el índice de la columna que le corresponde.
        /// </summary>
        private static (ConstructorInfo, int[]) BuscarConstructor(Type tipo, Miembro[] miembrosPorColumna)
        {
            ConstructorInfo[] constructores = tipo.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (ConstructorInfo constructor in constructores)
            {
                ParameterInfo[] parametros = constructor.GetParameters();

                if (parametros.Length != miembrosPorColumna.Length)
                    continue;

                int[] indices = new int[parametros.Length];
                bool coincide = true;

                for (int p = 0; p < parametros.Length && coincide; p++)
                {
                    int col = Array.FindIndex(miembrosPorColumna, m =>
                        string.Equals(m.Nombre, parametros[p].Name, StringComparison.OrdinalIgnoreCase) &&
                        m.Tipo == parametros[p].ParameterType);

                    if (col < 0)
                        coincide = false;
                    else
                        indices[p] = col;
                }

                if (coincide)
                    return (constructor, indices);
            }

            string esperado = string.Join(", ",
                miembrosPorColumna.Select(m => $"{m.Tipo.Name} {m.Nombre}"));

            throw new CsvConfiguracionException(
                $"No se encontró un constructor compatible para {tipo.Name}. " +
                $"Se necesita uno con los parámetros (en cualquier orden): ({esperado}). " +
                "El nombre de cada parámetro debe coincidir con el del miembro (sin distinguir mayúsculas) y el tipo debe ser idéntico.");
        }

        // ───────────────────────────── FORMATO ───────────────────────────────

        private static string FormatearValor(object? valor, Type tipo)
        {
            // Un valor null se escribe como la palabra null, sin comillas.
            if (valor == null)
                return NullTexto;

            Type real = Nullable.GetUnderlyingType(tipo) ?? tipo;

            // Un enum sin [Flags] cuyo valor no está definido se escribiría como un número
            // que Read rechazaría: se detecta al escribir y no al leer.
            if (real.IsEnum &&
                !real.IsDefined(typeof(FlagsAttribute), false) &&
                !Enum.IsDefined(real, valor))
            {
                throw new ArgumentException(
                    $"El valor {valor} no está definido en el enum {real.Name}; no podría leerse de vuelta.");
            }

            // string, char y enum van siempre entre comillas: un char puede ser una coma
            // y un enum con [Flags] se escribe como "A, B" (con coma).
            if (real == typeof(string) || real == typeof(char) || real.IsEnum)
                return $"\"{EscaparString(valor.ToString()!)}\"";

            // Formatos que se pueden leer de vuelta sin ambigüedad. El orden importa:
            // DateTime y similares también son IFormattable, y el caso general los taparía.
            return valor switch
            {
                DateTime v        => v.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset v  => v.ToString("O", CultureInfo.InvariantCulture),
                DateOnly v        => v.ToString("O", CultureInfo.InvariantCulture),
                TimeOnly v        => v.ToString("O", CultureInfo.InvariantCulture),
                TimeSpan v        => v.ToString("c", CultureInfo.InvariantCulture),
                IFormattable v    => v.ToString(null, CultureInfo.InvariantCulture),
                _                 => Convert.ToString(valor, CultureInfo.InvariantCulture)!
            };
        }

        // Reemplaza los caracteres especiales por su secuencia de escape, de modo que un
        // texto con comas, comillas o saltos de línea quede en una sola celda y una sola línea.
        private static string EscaparString(string texto)
        {
            StringBuilder resultado = new();

            foreach (char caracter in texto)
            {
                switch (caracter)
                {
                    case '\\': resultado.Append("\\\\"); break;
                    case '"':  resultado.Append("\\\""); break;
                    case '\n': resultado.Append("\\n");  break;
                    case '\r': resultado.Append("\\r");  break;
                    case '\t': resultado.Append("\\t");  break;
                    default:   resultado.Append(caracter); break;
                }
            }

            return resultado.ToString();
        }

        // ───────────────────────────── PARSEO ────────────────────────────────

        // Separa una línea en celdas respetando comillas y secuencias de escape.
        private static List<Celda> ParsearLinea(string linea, int numeroLinea)
        {
            List<Celda> celdas = new();
            StringBuilder valor = new();

            bool dentroDeString = false;   // dentro de comillas, la coma no separa celdas
            bool escapado = false;         // el carácter anterior fue una barra '\'
            bool huboComillas = false;     // la celda actual tuvo comillas (distingue "" de vacío)

            foreach (char caracter in linea)
            {
                if (escapado)
                {
                    valor.Append(caracter switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => caracter
                    });

                    escapado = false;
                    continue;
                }

                if (caracter == '\\')
                {
                    escapado = true;
                    continue;
                }

                if (caracter == '"')
                {
                    dentroDeString = !dentroDeString;
                    huboComillas = true;
                    continue;
                }

                if (caracter == ',' && !dentroDeString)
                {
                    celdas.Add(new Celda(valor.ToString(), huboComillas));
                    valor.Clear();
                    huboComillas = false;
                    continue;
                }

                valor.Append(caracter);
            }

            // Al terminar no puede quedar una barra pendiente ni comillas abiertas.
            if (escapado)
                throw new CsvFormatException("La línea termina con una barra '\\' incompleta.", numeroLinea);

            if (dentroDeString)
                throw new CsvFormatException("Hay comillas sin cerrar.", numeroLinea);

            celdas.Add(new Celda(valor.ToString(), huboComillas));

            return celdas;
        }

        // ─────────────────────────── CONVERSIÓN ──────────────────────────────

        // Convierte el texto de una celda al tipo del parámetro del constructor.
        private static object? ConvertirValor(Celda celda, Type tipo, int numeroLinea, string columna)
        {
            Type? subyacente = Nullable.GetUnderlyingType(tipo);
            bool aceptaNull = AceptaNull(tipo);
            Type real = subyacente ?? tipo;

            // Sin comillas, la palabra null (o una celda vacía) es null.
            // Con comillas ("null" o "") es un texto normal.
            if (!celda.EntreComillas && (celda.Texto == NullTexto || celda.Texto.Length == 0))
            {
                if (aceptaNull)
                    return null;

                throw new CsvFormatException(
                    $"La celda es null pero {tipo.Name} no admite null.",
                    numeroLinea, columna);
            }

            string texto = celda.Texto;
            CultureInfo inv = CultureInfo.InvariantCulture;

            try
            {
                if (real == typeof(string))
                    return texto;

                if (real == typeof(char))
                {
                    if (texto.Length != 1)
                        throw new FormatException("Se esperaba exactamente un carácter.");
                    return texto[0];
                }

                if (real.IsEnum)
                {
                    bool esFlags = real.IsDefined(typeof(FlagsAttribute), false);

                    string mensajeEnum =
                        $"'{texto}' no es un valor válido de {real.Name}. " +
                        $"Valores: {string.Join(", ", Enum.GetNames(real))}.";

                    object resultado;

                    try
                    {
                        resultado = Enum.Parse(real, texto, false);
                    }
                    catch (Exception ex) when (ex is ArgumentException or OverflowException)
                    {
                        throw new FormatException(mensajeEnum, ex);
                    }

                    // Enum.Parse acepta números que no corresponden a ningún valor
                    // (por ejemplo "99"); aquí se rechazan, salvo en enums [Flags].
                    if (!esFlags && !Enum.IsDefined(real, resultado))
                        throw new FormatException(mensajeEnum);

                    return resultado;
                }

                // Tipos que Convert.ChangeType no soporta.
                if (real == typeof(DateTime))
                    return DateTime.Parse(texto, inv, DateTimeStyles.RoundtripKind);
                if (real == typeof(DateTimeOffset))
                    return DateTimeOffset.Parse(texto, inv, DateTimeStyles.RoundtripKind);
                if (real == typeof(DateOnly))
                    return DateOnly.Parse(texto, inv);
                if (real == typeof(TimeOnly))
                    return TimeOnly.Parse(texto, inv);
                if (real == typeof(TimeSpan))
                    return TimeSpan.Parse(texto, inv);
                if (real == typeof(Guid))
                    return Guid.Parse(texto);
                if (real == typeof(bool))
                    return bool.Parse(texto);

                return Convert.ChangeType(texto, real, inv);
            }
            catch (CsvFormatException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is FormatException or OverflowException or InvalidCastException or ArgumentException)
            {
                throw new CsvFormatException(
                    $"No se pudo convertir '{texto}' a {real.Name}: {ex.Message}",
                    numeroLinea, columna, ex);
            }
        }
    }
}
