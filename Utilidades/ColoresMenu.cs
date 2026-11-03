using System;

namespace Utilidades
{
    /// <summary>Colores del texto y del fondo de los elementos de un menú.</summary>
    public struct ColoresMenu
    {
        public ConsoleColor Seleccion { get; set; }
        public ConsoleColor Cursor { get; set; }
        public ConsoleColor NoSeleccion { get; set; }
        public ConsoleColor TextoNormal { get; set; }
        public ConsoleColor FondoSeleccion { get; set; }
        public ConsoleColor FondoCursor { get; set; }
        public ConsoleColor FondoNoSeleccion { get; set; }
        public ConsoleColor FondoTextoNormal { get; set; }
        public ConsoleColor TextoMensaje { get; set; }
        public ConsoleColor FondoMensaje { get; set; }

        /// <summary>Inicializa los mismos colores predeterminados de los menús existentes.</summary>
        public ColoresMenu()
        {
            Seleccion = ConsoleColor.White;
            Cursor = ConsoleColor.Green;
            NoSeleccion = ConsoleColor.DarkGreen;
            TextoNormal = ConsoleColor.DarkGreen;
            FondoSeleccion = ConsoleColor.Black;
            FondoCursor = ConsoleColor.Black;
            FondoNoSeleccion = ConsoleColor.Black;
            FondoTextoNormal = ConsoleColor.Black;
            TextoMensaje = ConsoleColor.DarkGreen;
            FondoMensaje = ConsoleColor.Black;
        }
    }
}
