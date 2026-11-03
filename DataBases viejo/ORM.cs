// ============================================================================
//  ORM.cs  (parte de DataBases)
//  Resuelve relaciones entre objetos de tablas ya cargadas en memoria, a partir
//  de atributos declarados sobre propiedades.
//  Requisitos: .NET 8 o superior.
//
//  Los cuatro métodos públicos llevan "where T : class": una relación devuelve
//  un objeto o null, y con un struct "T?" tendría otro significado.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

using static DataBases.Atributos;

namespace DataBases
{
    public static class ORM
    {
        // --------------------------------------------------------------------
        //  Cachés de Reflection
        //
        //  Buscar una propiedad, su atributo o un campo con Reflection cuesta tiempo,
        //  y las relaciones se resuelven en cada acceso a la propiedad. Se guardan
        //  los resultados encontrados; los errores no se guardan, así que un error
        //  de configuración se vuelve a lanzar siempre.
        //  ConcurrentDictionary es seguro entre hilos.
        // --------------------------------------------------------------------
        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> cachePropiedades = new();
        private static readonly ConcurrentDictionary<(Type, string), FieldInfo> cacheCampos = new();
        private static readonly ConcurrentDictionary<(PropertyInfo, Type), Attribute> cacheAtributos = new();

        // ====================================================================
        //  1 → 1 directa
        // ====================================================================
        public static T? Relacionar_1_1<T>(
            object objeto,
            string nombrePropiedad,
            IEnumerable<T> tabla)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(objeto);
            ArgumentNullException.ThrowIfNull(tabla);

            Type tipoObjeto = objeto.GetType();

            PropertyInfo propiedad = ObtenerPropiedad(tipoObjeto, nombrePropiedad);
            Relacion_1_1Attribute relacion = ObtenerAtributo<Relacion_1_1Attribute>(propiedad, tipoObjeto);

            FieldInfo campoOrigen = ObtenerCampo(tipoObjeto, relacion.CampoOrigen);
            FieldInfo campoDestino = ObtenerCampo(typeof(T), relacion.CampoDestino);

            ValidarTipos(campoOrigen, campoDestino);

            object? valorOrigen = campoOrigen.GetValue(objeto);

            // Una clave foránea sin valor (por ejemplo int? = null) significa
            // "sin relación": no es un error.
            if (valorOrigen == null)
                return null;

            T? resultado = null;

            // La tabla se enumera directamente, sin copiarla: mientras se recorre solo se
            // ejecuta Reflection, no código del usuario.
            foreach (T? candidato in tabla)
            {
                // Un elemento null no puede relacionarse con nada: se omite.
                if (candidato == null)
                    continue;

                if (!ValoresIguales(valorOrigen, campoDestino.GetValue(candidato)))
                    continue;

                // Se sigue recorriendo aunque ya haya una coincidencia: así se detecta una
                // segunda, que sería una inconsistencia de los datos (por ejemplo, ids duplicados).
                if (resultado != null)
                {
                    throw new InvalidOperationException(
                        $"La relación 1→1 de {tipoObjeto.Name} " +
                        $"encontró más de un objeto relacionado en {typeof(T).Name}.");
                }

                resultado = candidato;
            }

            return resultado;
        }

        // ====================================================================
        //  1 → N directa
        // ====================================================================
        public static IReadOnlyList<T> Relacionar_1_N<T>(
            object objeto,
            string nombrePropiedad,
            IEnumerable<T> tabla)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(objeto);
            ArgumentNullException.ThrowIfNull(tabla);

            Type tipoObjeto = objeto.GetType();

            PropertyInfo propiedad = ObtenerPropiedad(tipoObjeto, nombrePropiedad);
            Relacion_1_NAttribute relacion = ObtenerAtributo<Relacion_1_NAttribute>(propiedad, tipoObjeto);

            FieldInfo campoOrigen = ObtenerCampo(tipoObjeto, relacion.CampoOrigen);
            FieldInfo campoDestino = ObtenerCampo(typeof(T), relacion.CampoDestino);

            ValidarTipos(campoOrigen, campoDestino);

            object? valorOrigen = campoOrigen.GetValue(objeto);

            if (valorOrigen == null)
                return Array.Empty<T>();

            List<T> resultado = new();

            foreach (T? candidato in tabla)
            {
                if (candidato == null)
                    continue;

                if (ValoresIguales(valorOrigen, campoDestino.GetValue(candidato)))
                    resultado.Add(candidato);
            }

            // Vista de solo lectura: quien usa la propiedad no puede modificar la
            // lista que representa la relación.
            return resultado.AsReadOnly();
        }

        // ====================================================================
        //  1 → 1 mediante tabla intermedia
        // ====================================================================
        public static T? Relacionar_1_1_Tabla<T>(
            object objeto,
            string nombrePropiedad,
            IEnumerable<object> tablaRelacion,
            IEnumerable<T> tablaDestino)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(objeto);
            ArgumentNullException.ThrowIfNull(tablaRelacion);
            ArgumentNullException.ThrowIfNull(tablaDestino);

            Type tipoObjeto = objeto.GetType();

            PropertyInfo propiedad = ObtenerPropiedad(tipoObjeto, nombrePropiedad);
            Relacion_1_1_TablaAttribute relacion = ObtenerAtributo<Relacion_1_1_TablaAttribute>(propiedad, tipoObjeto);

            List<T> encontrados = ResolverConTabla(
                objeto,
                relacion.TipoTabla,
                relacion.CampoOrigen,
                relacion.CampoTablaOrigen,
                relacion.CampoTablaDestino,
                relacion.CampoDestino,
                tablaRelacion,
                tablaDestino);

            if (encontrados.Count > 1)
            {
                throw new InvalidOperationException(
                    $"La relación 1→1 de {tipoObjeto.Name} " +
                    $"encontró más de un objeto relacionado en {typeof(T).Name}.");
            }

            return encontrados.Count == 0 ? null : encontrados[0];
        }

        // ====================================================================
        //  1 → N mediante tabla intermedia
        // ====================================================================
        public static IReadOnlyList<T> Relacionar_1_N_Tabla<T>(
            object objeto,
            string nombrePropiedad,
            IEnumerable<object> tablaRelacion,
            IEnumerable<T> tablaDestino)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(objeto);
            ArgumentNullException.ThrowIfNull(tablaRelacion);
            ArgumentNullException.ThrowIfNull(tablaDestino);

            Type tipoObjeto = objeto.GetType();

            PropertyInfo propiedad = ObtenerPropiedad(tipoObjeto, nombrePropiedad);
            Relacion_1_N_TablaAttribute relacion = ObtenerAtributo<Relacion_1_N_TablaAttribute>(propiedad, tipoObjeto);

            return ResolverConTabla(
                objeto,
                relacion.TipoTabla,
                relacion.CampoOrigen,
                relacion.CampoTablaOrigen,
                relacion.CampoTablaDestino,
                relacion.CampoDestino,
                tablaRelacion,
                tablaDestino).AsReadOnly();
        }

        // --------------------------------------------------------------------
        //  Núcleo común de las dos relaciones con tabla intermedia
        //
        //  Algoritmo en dos pasadas:
        //    1. Recorrer la tabla intermedia y juntar, en un HashSet, las claves de
        //       destino de las filas que pertenecen al objeto actual.
        //    2. Recorrer una vez la tabla destino y quedarse con los objetos cuya
        //       clave está en ese conjunto.
        //
        //  Consecuencias:
        //    · Si el objeto no tiene filas en la tabla intermedia, la tabla destino
        //      no se recorre.
        //    · Las filas repetidas de la tabla intermedia (1,5 / 1,5) no producen
        //      duplicados, porque el conjunto contiene claves y no objetos.
        //    · No depende de Equals de T: dos objetos distintos con los mismos
        //      valores no se fusionan.
        //    · El resultado sale en el orden de la tabla destino (determinista).
        //    · Si la tabla destino tiene dos objetos con la misma clave, se incluyen
        //      ambos sin error.
        // --------------------------------------------------------------------
        private static List<T> ResolverConTabla<T>(
            object objeto,
            Type tipoTabla,
            string nombreCampoOrigen,
            string nombreCampoTablaOrigen,
            string nombreCampoTablaDestino,
            string nombreCampoDestino,
            IEnumerable<object> tablaRelacion,
            IEnumerable<T> tablaDestino)
            where T : class
        {
            if (tipoTabla == null)
            {
                throw new InvalidOperationException(
                    $"El atributo de relación de {objeto.GetType().Name} no indica el tipo de la tabla intermedia.");
            }

            FieldInfo campoOrigen = ObtenerCampo(objeto.GetType(), nombreCampoOrigen);
            FieldInfo campoTablaOrigen = ObtenerCampo(tipoTabla, nombreCampoTablaOrigen);
            FieldInfo campoTablaDestino = ObtenerCampo(tipoTabla, nombreCampoTablaDestino);
            FieldInfo campoDestino = ObtenerCampo(typeof(T), nombreCampoDestino);

            ValidarTipos(campoOrigen, campoTablaOrigen);
            ValidarTipos(campoTablaDestino, campoDestino);

            object? valorOrigen = campoOrigen.GetValue(objeto);

            if (valorOrigen == null)
                return new List<T>();

            // Paso 1: claves de destino alcanzadas desde este objeto.
            HashSet<object> claves = new();

            foreach (object? fila in tablaRelacion)
            {
                if (fila == null)
                    continue;

                ValidarTipoTabla(fila, tipoTabla);

                if (!ValoresIguales(valorOrigen, campoTablaOrigen.GetValue(fila)))
                    continue;

                object? claveDestino = campoTablaDestino.GetValue(fila);

                if (claveDestino != null)
                    claves.Add(claveDestino);
            }

            List<T> resultado = new();

            if (claves.Count == 0)
                return resultado;

            // Paso 2: una sola pasada por la tabla destino.
            foreach (T? candidato in tablaDestino)
            {
                if (candidato == null)
                    continue;

                object? valorDestino = campoDestino.GetValue(candidato);

                if (valorDestino != null && claves.Contains(valorDestino))
                    resultado.Add(candidato);
            }

            return resultado;
        }

        // --------------------------------------------------------------------
        //  Reflection: propiedades, atributos y campos
        // --------------------------------------------------------------------

        private static PropertyInfo ObtenerPropiedad(Type tipo, string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException(
                    "El nombre de la propiedad no puede estar vacío. Use nameof(Propiedad).",
                    "nombrePropiedad");
            }

            (Type, string) clave = (tipo, nombre);

            if (cachePropiedades.TryGetValue(clave, out PropertyInfo? guardada))
                return guardada;

            // Se recorre la jerarquía desde la clase más derivada hacia la base, mirando
            // solo lo declarado en cada nivel (DeclaredOnly) y usando la primera que aparece:
            //  · si una clase derivada oculta la propiedad con "new", una búsqueda única
            //    lanzaría AmbiguousMatchException;
            //  · una propiedad privada de una clase base no se devuelve al consultar la
            //    clase derivada, pero sí al consultar la base directamente.
            // NonPublic permite que la propiedad tenga cualquier visibilidad.
            for (Type? actual = tipo; actual != null; actual = actual.BaseType)
            {
                PropertyInfo? propiedad = actual.GetProperty(
                    nombre,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (propiedad != null)
                {
                    cachePropiedades[clave] = propiedad;
                    return propiedad;
                }
            }

            throw new InvalidOperationException(
                $"No existe la propiedad de instancia {nombre} en el tipo {tipo.Name}.");
        }

        // Lee el atributo pedido de la propiedad; si no lo tiene, lanza un error claro
        // en lugar de un NullReferenceException.
        private static TAtributo ObtenerAtributo<TAtributo>(PropertyInfo propiedad, Type tipoObjeto)
            where TAtributo : Attribute
        {
            (PropertyInfo, Type) clave = (propiedad, typeof(TAtributo));

            if (cacheAtributos.TryGetValue(clave, out Attribute? guardado))
                return (TAtributo)guardado;

            TAtributo atributo =
                propiedad.GetCustomAttribute<TAtributo>()
                ?? throw new InvalidOperationException(
                    $"La propiedad {propiedad.Name} de {tipoObjeto.Name} " +
                    $"no tiene el atributo {typeof(TAtributo).Name}.");

            cacheAtributos[clave] = atributo;
            return atributo;
        }

        private static FieldInfo ObtenerCampo(Type tipo, string nombre)
        {
            // Un atributo con un nombre null o vacío se informa aquí, indicando el tipo,
            // en lugar de fallar más adentro con un error sin contexto.
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new InvalidOperationException(
                    $"Un atributo de relación indica un nombre de campo vacío para el tipo {tipo.Name}.");
            }

            (Type, string) clave = (tipo, nombre);

            if (cacheCampos.TryGetValue(clave, out FieldInfo? guardado))
                return guardado;

            FieldInfo? campo = BuscarCampo(tipo, nombre);

            if (campo == null)
            {
                // El error más frecuente es escribir el nombre de una propiedad ("Id") o
                // con otras mayúsculas: se listan los campos reales para corregirlo enseguida.
                throw new InvalidOperationException(
                    $"No existe el campo '{nombre}' en el tipo {tipo.Name} " +
                    $"ni en ninguno de sus tipos base. Se buscan CAMPOS (no propiedades) " +
                    $"y se distinguen mayúsculas. Campos disponibles: {ListarCampos(tipo)}.");
            }

            cacheCampos[clave] = campo;
            return campo;
        }

        // Busca un campo de instancia en el tipo y en sus clases base.
        // GetField con NonPublic no devuelve los campos privados heredados, por eso se
        // consulta cada clase por separado (DeclaredOnly).
        private static FieldInfo? BuscarCampo(Type tipo, string nombre)
        {
            Type? tipoActual = tipo;

            while (tipoActual != null)
            {
                FieldInfo? campo =
                    tipoActual.GetField(
                        nombre,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                if (campo != null)
                    return campo;

                tipoActual = tipoActual.BaseType;
            }

            return null;
        }

        // Nombres de los campos de instancia de un tipo y de sus bases, para los mensajes
        // de error. Se omiten los campos generados por el compilador (<X>k__BackingField).
        private static string ListarCampos(Type tipo)
        {
            List<string> nombres = new();

            for (Type? actual = tipo; actual != null && actual != typeof(object); actual = actual.BaseType)
            {
                foreach (FieldInfo campo in actual.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    if (!campo.Name.StartsWith('<'))
                        nombres.Add(campo.Name);
                }
            }

            return nombres.Count == 0 ? "(ninguno)" : string.Join(", ", nombres);
        }

        // --------------------------------------------------------------------
        //  Validaciones y comparación
        // --------------------------------------------------------------------

        // Comprueba que los dos campos comparados tengan el mismo tipo, sin considerar
        // Nullable<>: int? y int son compatibles. Así una clave foránea opcional
        // (int? clienteId frente a int id) funciona; al comparar, un int? con valor 5
        // se convierte en un int 5 "boxeado" y Equals(5, 5) es true.
        private static void ValidarTipos(FieldInfo campoOrigen, FieldInfo campoDestino)
        {
            Type tipoOrigen = Nullable.GetUnderlyingType(campoOrigen.FieldType) ?? campoOrigen.FieldType;
            Type tipoDestino = Nullable.GetUnderlyingType(campoDestino.FieldType) ?? campoDestino.FieldType;

            if (tipoOrigen != tipoDestino)
            {
                throw new InvalidOperationException(
                    $"Los campos no tienen tipos compatibles: " +
                    $"{Describir(campoOrigen)} y {Describir(campoDestino)}. " +
                    $"Deben ser del mismo tipo (se permite, por ejemplo, int con int?).");
            }
        }

        // Describe un campo como "Clase.campo (Tipo)", con "?" si es nullable.
        private static string Describir(FieldInfo campo)
        {
            Type? subyacente = Nullable.GetUnderlyingType(campo.FieldType);
            string tipo = subyacente != null ? subyacente.Name + "?" : campo.FieldType.Name;

            return $"{campo.DeclaringType?.Name}.{campo.Name} ({tipo})";
        }

        // Comprueba que una fila de la tabla intermedia sea del tipo indicado en el atributo.
        private static void ValidarTipoTabla(object objeto, Type tipoEsperado)
        {
            if (!tipoEsperado.IsInstanceOfType(objeto))
            {
                throw new InvalidOperationException(
                    $"La tabla intermedia contiene un objeto de tipo " +
                    $"{objeto.GetType().Name}, pero se esperaba " +
                    $"{tipoEsperado.Name}.");
            }
        }

        // Compara dos claves: dos null solo son iguales entre sí; en otro caso se usa Equals.
        private static bool ValoresIguales(object? valor1, object? valor2)
        {
            if (valor1 == null || valor2 == null)
                return valor1 == null && valor2 == null;

            return Equals(valor1, valor2);
        }
    }
}
