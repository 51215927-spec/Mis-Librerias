// ============================================================================
//  Tabla.cs  (parte de DataBases)
//  Colecciones que devuelve y usa el Modelo:
//    · Tabla<T>            → una tabla cargada (se puede modificar libremente).
//    · ListaRelacionada<T> → resultado de una relación 1 → N.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DataBases
{
    /// <summary>Una tabla en memoria. Es una Collection&lt;T&gt;: Add, Remove, índice, foreach, LINQ.</summary>
    public class Tabla<T> : Collection<T> where T : class
    {
        public Tabla() { }

        public Tabla(IEnumerable<T> elementos)
        {
            ArgumentNullException.ThrowIfNull(elementos);

            foreach (T elemento in elementos)
            {
                ArgumentNullException.ThrowIfNull(elemento);
                Items.Add(elemento);   // Items evita pasar por InsertItem
            }
        }

        public void AgregarRango(IEnumerable<T> elementos)
        {
            ArgumentNullException.ThrowIfNull(elementos);
            foreach (T elemento in elementos)
                Add(elemento);
        }

        protected override void InsertItem(int index, T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            base.SetItem(index, item);
        }
    }

    /// <summary>
    /// Resultado de una relación 1 → N. Es una COPIA del resultado en el momento de la consulta.
    /// Add y Remove ejecutan los bloques AlAgregar / AlQuitar escritos por el programador en la
    /// configuración de la relación; si no se configuraron, lanzan NotSupportedException.
    /// El modelo NO modifica claves foráneas por su cuenta.
    /// Limitación: Clear ejecuta AlQuitar elemento por elemento. Si AlQuitar lanza una excepción en
    /// un elemento intermedio, las claves de los anteriores ya fueron modificadas y la lista no se vacía.
    /// </summary>
    public sealed class ListaRelacionada<T> : Collection<T> where T : class
    {
        private readonly object origen;
        private readonly string descripcion;
        private readonly Action<object, object>? alAgregar;
        private readonly Action<object, object>? alQuitar;

        internal ListaRelacionada(
            IEnumerable<T> elementos,
            object origen,
            string descripcion,
            Action<object, object>? alAgregar,
            Action<object, object>? alQuitar)
        {
            this.origen = origen;
            this.descripcion = descripcion;
            this.alAgregar = alAgregar;
            this.alQuitar = alQuitar;

            foreach (T elemento in elementos)
                Items.Add(elemento);
        }

        private NotSupportedException NoConfigurado(string operacion, string bloque) =>
            new($"No se puede {operacion} en {descripcion}: la relación no define .{bloque}(...) " +
                "en su configuración. Modifique la tabla y las claves foráneas directamente.");

        protected override void InsertItem(int index, T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (alAgregar == null) throw NoConfigurado("agregar", "AlAgregar");

            alAgregar(origen, item);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            if (alQuitar == null) throw NoConfigurado("quitar", "AlQuitar");

            alQuitar(origen, this[index]);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (alQuitar == null) throw NoConfigurado("reemplazar", "AlQuitar");
            if (alAgregar == null) throw NoConfigurado("reemplazar", "AlAgregar");

            alQuitar(origen, this[index]);
            alAgregar(origen, item);
            base.SetItem(index, item);
        }

        protected override void ClearItems()
        {
            if (alQuitar == null) throw NoConfigurado("vaciar", "AlQuitar");

            // Si alQuitar lanza una excepción, los elementos anteriores ya fueron procesados (ver el resumen de la clase).
            foreach (T elemento in new List<T>(Items))
                alQuitar(origen, elemento);

            base.ClearItems();
        }
    }
}
