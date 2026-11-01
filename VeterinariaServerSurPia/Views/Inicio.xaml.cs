
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class Inicio : Page
    {
        public Inicio()
        {
            InitializeComponent();
        }

        private void BtnClientes_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Clientes");
        }

        private void BtnMascotas_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Mascotas");
        }

        private void BtnCitas_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Citas");
        }

        private void BtnHistorial_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("HistorialMedico");
        }

        private void BtnBusqueda_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Busqueda");
        }

        private void BtnModificar_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("ModificarEliminar");
        }

        private void BtnCalendario_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("CalendarioCitas");
        }

        private void BtnPDF_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("ExportarPDF");
        }
    }
}
