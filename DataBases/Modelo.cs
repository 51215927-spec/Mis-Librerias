// ============================================================================
//  Modelo.cs  (parte de DataBases)
//  El modelo ya validado y construido por ConstructorModelo.Construir():
//  guarda la configuración (archivos, columnas, relaciones) y las tablas cargadas,
//  y resuelve las relaciones entre objetos.
//  Requisitos: .NET 8 o superior.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DataBases
{
    public enum Cardinalidad { Uno, Muchos }

    // ───────────── Piezas internas del modelo (inmutables) ─────────────

    /// <summary>Un campo o una propiedad obtenido de una expresión lambda (c => c.Id).</summary>
    internal sealed class Miembro
    {
        public MemberInfo Info { get; }
        public Type Tipo { get; }
        public string Nombre => Info.Name;

        private Miembro(MemberInfo info, Type tipo)
        {
            Info = info;
            Tipo = tipo;
        }

        public object? Leer(object objeto) => Info switch
        {
            FieldInfo f => f.GetValue(objeto),
            PropertyInfo p => p.GetValue(objeto),
            _ => throw new InvalidOperationException("Miembro no soportado.")
        };

        /// <summary>Extrae el miembro de una lambda. Solo acepta un acceso directo: x => x.Miembro.</summary>
        public static Miembro Desde(LambdaExpression lambda, string contexto)
        {
            Expression cuerpo = lambda.Body;

            // Las conversiones (por ejemplo int → object por boxing) no cambian el miembro.
            while (cuerpo is UnaryExpression u &&
                   u.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.TypeAs)
                cuerpo = u.Operand;

            if (cuerpo is not MemberExpression m ||
                !ReferenceEquals(m.Expression, lambda.Parameters[0]))
            {
                throw new CsvConfiguracionException(
                    $"{contexto}: la expresión '{lambda}' debe ser un acceso directo a un campo o " +
                    "propiedad del objeto (por ejemplo x => x.Id).");
            }

            return m.Member switch
            {
                FieldInfo f => new Miembro(f, f.FieldType),
                PropertyInfo p when p.CanRead && p.GetIndexParameters().Length == 0 =>
                    new Miembro(p, p.PropertyType),
                _ => throw new CsvConfiguracionException(
                    $"{contexto}: '{m.Member.Name}' no es un campo ni una propiedad legible.")
            };
        }

        public string Describir()
        {
            Type? sub = Nullable.GetUnderlyingType(Tipo);
            string tipo = sub != null ? sub.Name + "?" : Tipo.Name;
            return $"{Info.DeclaringType?.Name}.{Nombre} ({tipo})";
        }

        /// <summary>Los dos miembros deben tener el mismo tipo sin contar Nullable: int e int? son compatibles.</summary>
        public static void ValidarCompatibles(Miembro a, Miembro b, string contexto)
        {
            Type ta = Nullable.GetUnderlyingType(a.Tipo) ?? a.Tipo;
            Type tb = Nullable.GetUnderlyingType(b.Tipo) ?? b.Tipo;

            if (ta != tb)
                throw new CsvConfiguracionException(
                    $"{contexto}: los miembros no tienen tipos compatibles: {a.Describir()} y {b.Describir()}. " +
                    "Deben ser del mismo tipo (se permite, por ejemplo, int con int?).");
        }
    }

    internal sealed record ColumnaModelo(Miembro Miembro, string Nombre);

    /// <summary>Marca del archivo en disco al cargarlo (fecha de modificación y tamaño).</summary>
    internal readonly record struct VersionArchivo(long Ticks, long Longitud);

    internal sealed class RelacionModelo
    {
        public required string Nombre { get; init; }
        public required Cardinalidad Cardinalidad { get; init; }
        public required Type TipoDestino { get; init; }
        public Type? TipoTabla { get; init; }              // null = relación directa
        public required Miembro Origen { get; init; }
        public Miembro? TablaOrigen { get; init; }
        public Miembro? TablaDestino { get; init; }
        public required Miembro Destino { get; init; }
        public Action<object, object>? AlAgregar { get; init; }
        public Action<object, object>? AlQuitar { get; init; }
    }

    internal sealed class EntidadModelo
    {
        private readonly Dictionary<string, RelacionModelo> relaciones = new();

        public Type Tipo { get; }
        public string Ruta { get; }
        public IReadOnlyList<ColumnaModelo> Columnas { get; }
        public IReadOnlyDictionary<string, RelacionModelo> Relaciones => relaciones;

        /// <summary>Versión del archivo cuando se cargó o guardó por última vez (null = no existía).
        /// Permite detectar que otro proceso lo modificó.</summary>
        public VersionArchivo? Version { get; set; }

        // Capturan el tipo T de la entidad: permiten leer y escribir sin conocerlo.
        public Func<EntidadModelo, bool, IList> Leer { get; }
        public Action<EntidadModelo, IList> Escribir { get; }

        public EntidadModelo(
            Type tipo, string ruta, IReadOnlyList<ColumnaModelo> columnas,
            Func<EntidadModelo, bool, IList> leer, Action<EntidadModelo, IList> escribir)
        {
            Tipo = tipo;
            Ruta = ruta;
            Columnas = columnas;
            Leer = leer;
            Escribir = escribir;
        }

        public void AgregarRelacion(RelacionModelo relacion) => relaciones.Add(relacion.Nombre, relacion);
    }

    // ───────────────────────────── Modelo ──────────────────────────────

    public sealed class Modelo
    {
        private readonly IReadOnlyDictionary<Type, EntidadModelo> entidades;
        private readonly Dictionary<Type, IList> tablas = new();

        internal Modelo(IReadOnlyDictionary<Type, EntidadModelo> entidades) =>
            this.entidades = entidades;

        // ───────────── Tablas ─────────────

        /// <summary>Lee el CSV de T y deja la tabla en el modelo. Si el archivo no existe devuelve
        /// una tabla vacía, salvo que exigirArchivo sea true. Si la tabla ya estaba cargada se
        /// reutiliza la misma instancia (se vacía y se vuelve a llenar).</summary>
        public Tabla<T> Cargar<T>(bool exigirArchivo = false) where T : class
        {
            EntidadModelo e = ObtenerEntidad(typeof(T));
            return (Tabla<T>)Recargar(e, exigirArchivo);
        }

        /// <summary>Carga todas las entidades registradas. Los archivos inexistentes dan tablas vacías.
        /// Bloquea todos los archivos juntos para que otro proceso no guarde a mitad de la carga.</summary>
        public void CargarTodo()
        {
            using (CsvManager.Bloquear(ObtenerRutas()))
            {
                foreach (EntidadModelo e in entidades.Values)
                    Recargar(e, false);
            }
        }

        public void Guardar<T>() where T : class
        {
            EntidadModelo e = ObtenerEntidad(typeof(T));
            e.Escribir(e, ObtenerTabla(typeof(T)));
        }

        /// <summary>Guarda todas las tablas. Bloquea todos los archivos juntos para que otro proceso
        /// no lea un conjunto a medio guardar. No es una transacción: si falla a mitad, los archivos
        /// ya escritos quedan guardados.</summary>
        public void GuardarTodo()
        {
            using (CsvManager.Bloquear(ObtenerRutas()))
            {
                foreach (EntidadModelo e in entidades.Values)
                    e.Escribir(e, ObtenerTabla(e.Tipo));
            }
        }

        /// <summary>La tabla cargada de T. Se puede modificar y luego guardar con Guardar&lt;T&gt;().</summary>
        public Tabla<T> Tabla<T>() where T : class => (Tabla<T>)ObtenerTabla(typeof(T));

        // ───────────── Relaciones ─────────────
        //
        // El nombre de la propiedad lo aporta el compilador ([CallerMemberName]):
        // se llama desde la propiedad de navegación, sin escribir su nombre.
        //
        //   public Cliente? Cliente => Datos.Modelo.Uno<Cliente>(this);

        public TDestino? Uno<TDestino>(object origen, [CallerMemberName] string? propiedad = null)
            where TDestino : class
        {
            ArgumentNullException.ThrowIfNull(origen);
            RelacionModelo r = ObtenerRelacion(origen, propiedad, Cardinalidad.Uno, typeof(TDestino));
            List<TDestino> encontrados = Resolver<TDestino>(origen, r);

            if (encontrados.Count > 1)
                throw new InvalidOperationException(
                    $"La relación 1→1 {origen.GetType().Name}.{propiedad} " +
                    $"encontró más de un objeto relacionado en {typeof(TDestino).Name}.");

            return encontrados.Count == 0 ? null : encontrados[0];
        }

        public ListaRelacionada<TDestino> Muchos<TDestino>(object origen, [CallerMemberName] string? propiedad = null)
            where TDestino : class
        {
            ArgumentNullException.ThrowIfNull(origen);
            RelacionModelo r = ObtenerRelacion(origen, propiedad, Cardinalidad.Muchos, typeof(TDestino));

            return new ListaRelacionada<TDestino>(
                Resolver<TDestino>(origen, r),
                origen,
                $"{origen.GetType().Name}.{propiedad}",
                r.AlAgregar,
                r.AlQuitar);
        }

        // ───────────── Internos ─────────────

        // Lee la tabla y conserva la instancia ya cargada (si la hay), para que las variables que
        // la guardaban no queden desactualizadas. Si la lectura falla, la tabla anterior no cambia.
        private IList Recargar(EntidadModelo e, bool exigirArchivo)
        {
            IList nueva = e.Leer(e, exigirArchivo);

            if (!tablas.TryGetValue(e.Tipo, out IList? actual))
            {
                tablas[e.Tipo] = nueva;
                return nueva;
            }

            actual.Clear();
            foreach (object? elemento in nueva)
                actual.Add(elemento);

            return actual;
        }

        private List<string> ObtenerRutas()
        {
            List<string> rutas = new();

            foreach (EntidadModelo e in entidades.Values)
                rutas.Add(e.Ruta);

            return rutas;
        }

        private List<TDestino> Resolver<TDestino>(object origen, RelacionModelo r) where TDestino : class
        {
            List<TDestino> resultado = new();
            object? valorOrigen = r.Origen.Leer(origen);

            // Clave sin valor (por ejemplo int? = null) = "sin relación": no es un error.
            if (valorOrigen == null)
                return resultado;

            IList destino = ObtenerTabla(r.TipoDestino);

            if (r.TipoTabla == null)
            {
                foreach (object? c in destino)
                    if (c is TDestino t && Equals(valorOrigen, r.Destino.Leer(t)))
                        resultado.Add(t);

                return resultado;
            }

            // Con tabla intermedia, dos pasadas:
            //  1. juntar las claves de destino de las filas del objeto actual;
            //  2. recorrer la tabla destino una vez y quedarse con las que coinciden.
            HashSet<object> claves = new();

            foreach (object? fila in ObtenerTabla(r.TipoTabla))
            {
                if (fila == null || !Equals(valorOrigen, r.TablaOrigen!.Leer(fila)))
                    continue;

                object? clave = r.TablaDestino!.Leer(fila);
                if (clave != null)
                    claves.Add(clave);
            }

            if (claves.Count == 0)
                return resultado;

            foreach (object? c in destino)
            {
                if (c is not TDestino t) continue;

                object? valor = r.Destino.Leer(t);
                if (valor != null && claves.Contains(valor))
                    resultado.Add(t);
            }

            return resultado;
        }

        private RelacionModelo ObtenerRelacion(
            object objeto, string? propiedad, Cardinalidad esperada, Type destinoEsperado)
        {
            if (string.IsNullOrWhiteSpace(propiedad))
                throw new ArgumentException("El nombre de la propiedad no puede estar vacío.", nameof(propiedad));

            // Desde el tipo real hacia las clases base (herencia).
            for (Type? t = objeto.GetType(); t != null; t = t.BaseType)
            {
                if (!entidades.TryGetValue(t, out EntidadModelo? e) ||
                    !e.Relaciones.TryGetValue(propiedad, out RelacionModelo? r))
                    continue;

                if (r.Cardinalidad != esperada)
                    throw new InvalidOperationException(
                        $"La relación {t.Name}.{propiedad} es {r.Cardinalidad}; se la resolvió como {esperada}. " +
                        "Use Uno<T> para relaciones 1→1 y Muchos<T> para 1→N.");

                if (r.TipoDestino != destinoEsperado)
                    throw new InvalidOperationException(
                        $"La relación {t.Name}.{propiedad} apunta a {r.TipoDestino.Name}, no a {destinoEsperado.Name}.");

                return r;
            }

            throw new InvalidOperationException(
                $"El modelo no define la relación '{propiedad}' para {objeto.GetType().Name}.");
        }

        private EntidadModelo ObtenerEntidad(Type tipo) =>
            entidades.TryGetValue(tipo, out EntidadModelo? e)
                ? e
                : throw new InvalidOperationException($"{tipo.Name} no está registrada en el modelo.");

        private IList ObtenerTabla(Type tipo) =>
            tablas.TryGetValue(tipo, out IList? t)
                ? t
                : throw new InvalidOperationException(
                    $"La tabla de {tipo.Name} no fue cargada. Llame a Cargar<{tipo.Name}>() o CargarTodo().");
    }
}
