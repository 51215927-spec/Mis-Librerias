namespace Utilidades
{
    /// <summary>Presentación y navegación de un menú; los colores se configuran por separado.</summary>
    public struct FormatoMenu
    {
        public int SeleccionInicial { get; set; }
        public int Ancho { get; set; }
        public string Marcador { get; set; }
        public string Ayuda { get; set; }
        public string Pie { get; set; }
        public bool NumerarOpciones { get; set; }
        public bool NavegacionCircular { get; set; }
        public bool SeleccionNumerica { get; set; }

        public FormatoMenu()
        {
            SeleccionInicial = 0;
            Ancho = 0;
            Marcador = "►";
            Ayuda = "Usá ↑ / ↓ para navegar y ENTER para seleccionar.";
            Pie = "";
            NumerarOpciones = true;
            NavegacionCircular = true;
            SeleccionNumerica = false;
        }
    }
}
