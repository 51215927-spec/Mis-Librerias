// ============================================================================
//  ConstructorModelo.cs  (parte de DataBases)
//  Configuración del modelo con expresiones lambda (sin textos ni atributos).
//
//  Cada entidad implementa IEntidad<T> y declara su configuración en un método
//  estático dentro de la propia clase, donde se ven sus miembros privados:
//
//      public static void Configurar(ConstructorEntidad<Venta> e) =>
//          e.Archivo("datos/ventas.csv")
//           .Columna(v => v.id)
//           .Columna(v => v.clienteId);
//
//  Requisitos: .NET 8 o superior (miembros abstractos estáticos en interfaces).
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace DataBases
{
    /// <summary>La entidad se configura a sí misma.</summary>
    public interface IEntidad<TSelf> where TSelf : class, IEntidad<TSelf>
    {
        static abstract void Configurar(ConstructorEntidad<TSelf> entidad);
    }

    internal interface IConstructorEntidad
    {
        Type Tipo { get; }
        EntidadModelo ConstruirEntidad();
        void ConstruirRelaciones(EntidadModelo origen, IReadOnlyDictionary<Type, EntidadModelo> todas);
    }

    internal interface IConstructorRelacion
    {
        RelacionModelo Construir(Type tipoOrigen, IReadOnlyDictionary<Type, EntidadModelo> todas);
    }

    // ───────────────────────── ConstructorModelo ─────────────────────────

    public sealed class ConstructorModelo
    {
        private readonly Dictionary<Type, IConstructorEntidad> entidades = new();

        /// <summary>Registra la entidad y ejecuta su método estático Configurar.</summary>
        public ConstructorModelo Agregar<T>() where T : class, IEntidad<T>
        {
            if (entidades.ContainsKey(typeof(T)))
                throw new CsvConfiguracionException($"{typeof(T).Name} ya fue agregada al modelo.");

            ConstructorEntidad<T> constructor = new();
            T.Configurar(constructor);
            entidades[typeof(T)] = constructor;
            return this;
        }

        /// <summary>Valida todo y devuelve el modelo. Lanza CsvConfiguracionException.</summary>
        public Modelo Construir()
        {
            Dictionary<Type, EntidadModelo> resultado = new();

            // Fase 1: archivo, columnas y constructor de cada entidad.
            foreach (IConstructorEntidad c in entidades.Values)
                resultado[c.Tipo] = c.ConstruirEntidad();

            // Dos entidades con el mismo archivo se pisarían al guardar.
            var repetidas = resultado.Values
                .GroupBy(e => e.Ruta, CsvManager.ComparadorRutas)
                .Where(g => g.Count() > 1)
                .ToArray();

            if (repetidas.Length > 0)
                throw new CsvConfiguracionException(
                    $"Hay entidades que usan el mismo archivo ({repetidas[0].Key}): " +
                    string.Join(", ", repetidas[0].Select(e => e.Tipo.Name)) + ".");

            // Fase 2: relaciones (necesitan todas las entidades ya registradas).
            foreach (IConstructorEntidad c in entidades.Values)
                c.ConstruirRelaciones(resultado[c.Tipo], resultado);

            return new Modelo(resultado);
        }
    }

    // ───────────────────────── ConstructorEntidad ─────────────────────────

    public sealed class ConstructorEntidad<T> : IConstructorEntidad where T : class
    {
        private string? ruta;
        private readonly List<(Miembro Miembro, string Nombre)> columnas = new();
        private readonly List<IConstructorRelacion> relaciones = new();

        internal ConstructorEntidad() { }

        Type IConstructorEntidad.Tipo => typeof(T);

        /// <summary>Ruta del CSV de esta entidad. Una ruta relativa se convierte a absoluta ahora, contra
        /// el directorio de trabajo actual. Para que dos programas compartan el archivo conviene
        /// indicar una ruta absoluta común.</summary>
        public ConstructorEntidad<T> Archivo(string ruta)
        {
            this.ruta = string.IsNullOrWhiteSpace(ruta) ? ruta : Path.GetFullPath(ruta);
            return this;
        }

        /// <summary>Un miembro guardado en el CSV. Si no se indica nombre, la columna se llama
        /// igual que el miembro. El miembro puede ser un campo o una propiedad, de cualquier visibilidad.</summary>
        public ConstructorEntidad<T> Columna<TValor>(Expression<Func<T, TValor>> miembro, string? nombre = null)
        {
            ArgumentNullException.ThrowIfNull(miembro);

            Miembro m = Miembro.Desde(miembro, $"{typeof(T).Name}.Columna");
            columnas.Add((m, nombre ?? m.Nombre));
            return this;
        }

        /// <summary>Relación 1 → 1. La propiedad de navegación es de tipo TDestino.</summary>
        public ConstructorRelacion<T, TDestino> Uno<TDestino>(Expression<Func<T, TDestino?>> navegacion)
            where TDestino : class
        {
            ArgumentNullException.ThrowIfNull(navegacion);

            ConstructorRelacion<T, TDestino> r =
                new(Cardinalidad.Uno, Miembro.Desde(navegacion, $"{typeof(T).Name}.Uno"));
            relaciones.Add(r);
            return r;
        }

        /// <summary>Relación 1 → N. La propiedad de navegación es una colección de TDestino
        /// (ListaRelacionada&lt;TDestino&gt;, IList, ICollection o IEnumerable).</summary>
        public ConstructorRelacion<T, TDestino> Muchos<TDestino>(Expression<Func<T, IEnumerable<TDestino>>> navegacion)
            where TDestino : class
        {
            ArgumentNullException.ThrowIfNull(navegacion);

            ConstructorRelacion<T, TDestino> r =
                new(Cardinalidad.Muchos, Miembro.Desde(navegacion, $"{typeof(T).Name}.Muchos"));
            relaciones.Add(r);
            return r;
        }

        // ───────────── Construcción (internos) ─────────────

        EntidadModelo IConstructorEntidad.ConstruirEntidad()
        {
            string nombreTipo = typeof(T).Name;

            if (string.IsNullOrWhiteSpace(ruta))
                throw new CsvConfiguracionException(
                    $"La entidad {nombreTipo} no indica su archivo. Use .Archivo(\"ruta.csv\").");

            if (columnas.Count == 0)
                throw new CsvConfiguracionException($"La entidad {nombreTipo} no declara ninguna columna.");

            List<ColumnaModelo> resultado = new();

            foreach ((Miembro miembro, string nombre) in columnas)
            {
                if (string.IsNullOrWhiteSpace(nombre) ||
                    nombre.IndexOfAny(new[] { ',', '"', '\\', '\n', '\r' }) >= 0)
                    throw new CsvConfiguracionException(
                        $"El nombre de columna '{nombre}' ({nombreTipo}.{miembro.Nombre}) está vacío o contiene " +
                        "caracteres no permitidos (coma, comillas, barra o salto de línea).");

                if (!CsvManager.EsTipoSoportado(miembro.Tipo))
                    throw new CsvConfiguracionException(
                        $"El tipo {miembro.Tipo.Name} del miembro {nombreTipo}.{miembro.Nombre} no está soportado.");

                resultado.Add(new ColumnaModelo(miembro, nombre));
            }

            string[] nombresRepetidos = resultado.GroupBy(c => c.Nombre)
                .Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            if (nombresRepetidos.Length > 0)
                throw new CsvConfiguracionException(
                    $"{nombreTipo} tiene nombres de columna repetidos: {string.Join(", ", nombresRepetidos)}.");

            if (resultado.Select(c => c.Miembro.Nombre).Distinct().Count() != resultado.Count)
                throw new CsvConfiguracionException(
                    $"{nombreTipo} declara el mismo miembro en más de una columna.");

            EntidadModelo entidad = new(
                typeof(T), ruta!, resultado,
                (e, exigir) => new Tabla<T>(CsvManager.Leer<T>(e, exigir)),
                (e, lista) => CsvManager.Escribir(e, (Tabla<T>)lista));

            // Se exige un constructor compatible desde ahora, no recién al leer o escribir.
            CsvManager.ValidarConstructor(entidad);
            return entidad;
        }

        void IConstructorEntidad.ConstruirRelaciones(
            EntidadModelo origen, IReadOnlyDictionary<Type, EntidadModelo> todas)
        {
            foreach (IConstructorRelacion r in relaciones)
            {
                RelacionModelo modelo = r.Construir(typeof(T), todas);

                if (origen.Relaciones.ContainsKey(modelo.Nombre))
                    throw new CsvConfiguracionException(
                        $"{typeof(T).Name} ya tiene una relación llamada '{modelo.Nombre}'.");

                origen.AgregarRelacion(modelo);
            }
        }
    }

    // ───────────────────────── ConstructorRelacion ─────────────────────────

    /// <summary>
    /// Define una relación. Ejemplos:
    ///   directa:         .Con(v => v.clienteId).ApuntaA(c => c.id)
    ///   tabla intermedia:.Con(a => a.id).Por&lt;AlumnoMateria&gt;(t => t.alumnoId, t => t.materiaId).ApuntaA(m => m.id)
    /// </summary>
    public sealed class ConstructorRelacion<TOrigen, TDestino> : IConstructorRelacion
        where TOrigen : class where TDestino : class
    {
        private readonly Cardinalidad cardinalidad;
        private readonly Miembro navegacion;

        private Miembro? origen, destino, tablaOrigen, tablaDestino;
        private Type? tipoTabla;
        private Action<object, object>? alAgregar, alQuitar;

        internal ConstructorRelacion(Cardinalidad cardinalidad, Miembro navegacion)
        {
            this.cardinalidad = cardinalidad;
            this.navegacion = navegacion;
        }

        string Contexto => $"{typeof(TOrigen).Name}.{navegacion.Nombre}";

        /// <summary>Clave del objeto actual que participa en la relación.</summary>
        public ConstructorRelacion<TOrigen, TDestino> Con(Expression<Func<TOrigen, object?>> clave)
        {
            ArgumentNullException.ThrowIfNull(clave);
            origen = Miembro.Desde(clave, Contexto + ".Con");
            return this;
        }

        /// <summary>Tabla intermedia: primero la clave de la tabla que apunta al objeto actual,
        /// después la que apunta al destino.</summary>
        public ConstructorRelacion<TOrigen, TDestino> Por<TTabla>(
            Expression<Func<TTabla, object?>> haciaOrigen,
            Expression<Func<TTabla, object?>> haciaDestino) where TTabla : class
        {
            ArgumentNullException.ThrowIfNull(haciaOrigen);
            ArgumentNullException.ThrowIfNull(haciaDestino);

            tipoTabla = typeof(TTabla);
            tablaOrigen = Miembro.Desde(haciaOrigen, Contexto + ".Por");
            tablaDestino = Miembro.Desde(haciaDestino, Contexto + ".Por");
            return this;
        }

        /// <summary>Clave del objeto destino con la que se compara.</summary>
        public ConstructorRelacion<TOrigen, TDestino> ApuntaA(Expression<Func<TDestino, object?>> clave)
        {
            ArgumentNullException.ThrowIfNull(clave);
            destino = Miembro.Desde(clave, Contexto + ".ApuntaA");
            return this;
        }

        /// <summary>Solo 1 → N. Se ejecuta en Add sobre la lista de la relación. Es el programador
        /// quien actualiza las claves foráneas aquí.</summary>
        public ConstructorRelacion<TOrigen, TDestino> AlAgregar(Action<TOrigen, TDestino> accion)
        {
            ArgumentNullException.ThrowIfNull(accion);
            alAgregar = (o, d) => accion((TOrigen)o, (TDestino)d);
            return this;
        }

        /// <summary>Solo 1 → N. Se ejecuta en Remove y Clear sobre la lista de la relación.</summary>
        public ConstructorRelacion<TOrigen, TDestino> AlQuitar(Action<TOrigen, TDestino> accion)
        {
            ArgumentNullException.ThrowIfNull(accion);
            alQuitar = (o, d) => accion((TOrigen)o, (TDestino)d);
            return this;
        }

        RelacionModelo IConstructorRelacion.Construir(
            Type tipoOrigen, IReadOnlyDictionary<Type, EntidadModelo> todas)
        {
            string ctx = Contexto;

            if (origen == null)
                throw new CsvConfiguracionException($"{ctx}: falta .Con(...) (clave del objeto actual).");
            if (destino == null)
                throw new CsvConfiguracionException($"{ctx}: falta .ApuntaA(...) (clave del destino).");

            if (cardinalidad == Cardinalidad.Uno)
            {
                if (!navegacion.Tipo.IsAssignableFrom(typeof(TDestino)))
                    throw new CsvConfiguracionException(
                        $"{ctx}: la propiedad es de tipo {navegacion.Tipo.Name}, se esperaba {typeof(TDestino).Name}.");

                if (alAgregar != null || alQuitar != null)
                    throw new CsvConfiguracionException($"{ctx}: AlAgregar y AlQuitar solo valen en relaciones 1→N.");
            }
            else if (!navegacion.Tipo.IsAssignableFrom(typeof(ListaRelacionada<TDestino>)))
            {
                throw new CsvConfiguracionException(
                    $"{ctx}: la propiedad es de tipo {navegacion.Tipo.Name}; debe poder recibir " +
                    $"un ListaRelacionada<{typeof(TDestino).Name}> (use ese tipo, IList, ICollection o IEnumerable).");
            }

            if (!todas.ContainsKey(typeof(TDestino)))
                throw new CsvConfiguracionException(
                    $"{ctx}: apunta a {typeof(TDestino).Name}, que no está agregada con Agregar<{typeof(TDestino).Name}>().");

            if (tipoTabla != null && !todas.ContainsKey(tipoTabla))
                throw new CsvConfiguracionException(
                    $"{ctx}: usa la tabla intermedia {tipoTabla.Name}, que no está agregada al modelo.");

            if (tipoTabla == null)
            {
                Miembro.ValidarCompatibles(origen, destino, ctx);
            }
            else
            {
                Miembro.ValidarCompatibles(origen, tablaOrigen!, ctx);
                Miembro.ValidarCompatibles(tablaDestino!, destino, ctx);
            }

            return new RelacionModelo
            {
                Nombre = navegacion.Nombre,
                Cardinalidad = cardinalidad,
                TipoDestino = typeof(TDestino),
                TipoTabla = tipoTabla,
                Origen = origen,
                TablaOrigen = tablaOrigen,
                TablaDestino = tablaDestino,
                Destino = destino,
                AlAgregar = alAgregar,
                AlQuitar = alQuitar
            };
        }
    }
}
