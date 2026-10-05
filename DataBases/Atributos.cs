using System;

namespace DataBases
{
    public static class Atributos
    {
        [AttributeUsage(AttributeTargets.Field)]
        public class ColumnaAttribute : Attribute
        {
            public string Nombre { get; }

            public ColumnaAttribute(string nombre)
            {
                Nombre = nombre;
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class Relacion_1_1Attribute : Attribute
        {
            public string CampoOrigen { get; }
            public string CampoDestino { get; }

            public Relacion_1_1Attribute(
                string campoOrigen,
                string campoDestino)
            {
                CampoOrigen = campoOrigen;
                CampoDestino = campoDestino;
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class Relacion_1_NAttribute : Attribute
        {
            public string CampoOrigen { get; }
            public string CampoDestino { get; }

            public Relacion_1_NAttribute(
                string campoOrigen,
                string campoDestino)
            {
                CampoOrigen = campoOrigen;
                CampoDestino = campoDestino;
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class Relacion_1_1_TablaAttribute : Attribute
        {
            public Type TipoTabla { get; }

            public string CampoOrigen { get; }
            public string CampoTablaOrigen { get; }
            public string CampoTablaDestino { get; }
            public string CampoDestino { get; }

            public Relacion_1_1_TablaAttribute(
                Type tipoTabla,
                string campoOrigen,
                string campoTablaOrigen,
                string campoTablaDestino,
                string campoDestino)
            {
                TipoTabla = tipoTabla;
                CampoOrigen = campoOrigen;
                CampoTablaOrigen = campoTablaOrigen;
                CampoTablaDestino = campoTablaDestino;
                CampoDestino = campoDestino;
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class Relacion_1_N_TablaAttribute : Attribute
        {
            public Type TipoTabla { get; }

            public string CampoOrigen { get; }
            public string CampoTablaOrigen { get; }
            public string CampoTablaDestino { get; }
            public string CampoDestino { get; }

            public Relacion_1_N_TablaAttribute(
                Type tipoTabla,
                string campoOrigen,
                string campoTablaOrigen,
                string campoTablaDestino,
                string campoDestino)
            {
                TipoTabla = tipoTabla;
                CampoOrigen = campoOrigen;
                CampoTablaOrigen = campoTablaOrigen;
                CampoTablaDestino = campoTablaDestino;
                CampoDestino = campoDestino;
            }
        }
    }
}
