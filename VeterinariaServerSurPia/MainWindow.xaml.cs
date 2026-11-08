
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using VeterinariaServerSurPia.Views;

namespace VeterinariaServerSurPia
{
    public sealed partial class MainWindow : Window
    {
        public static MainWindow Current { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Title = "Servetsur Mty";
            Current = this;
            AplicarIconoVentana();
            LoginFrame.Navigate(typeof(LoginPage));
        }

        // Ícono de la ventana (barra de título, barra de tareas y Alt+Tab)
        private void AplicarIconoVentana()
        {
            try
            {
                string rutaIcono = System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "AppIcon.ico");

                if (System.IO.File.Exists(rutaIcono))
                {
                    AppWindow.SetIcon(rutaIcono);
                }
            }
            catch
            {
                // Si el ícono no se puede cargar, la app sigue funcionando.
            }
        }

        public static string CitaPendienteId { get; set; } = "";



        public void MostrarMenuPrincipal()
        {
            LoginFrame.Visibility = Visibility.Collapsed;
            NavView.Visibility = Visibility.Visible;
            SeleccionarPantalla("Inicio");
        }


        public void SeleccionarPantalla(string pantalla)
        {
            Type? destino = pantalla switch
            {

                "Inicio" => typeof(Inicio),
                "Clientes" => typeof(RegistrarCliente),
                "Mascotas" => typeof(RegistrarMascota),
                "Citas" => typeof(RegistrarCita),
                "Busqueda" => typeof(BusquedaConsulta),
                "ModificarEliminar" => typeof(ModificarEliminar),
                "Asistente" => typeof(AsistenteView),
                "HistorialMedico" => typeof(HistorialMedico),
                "ConsultarHistorial" => typeof(ConsultarHistorial),
                "CalendarioCitas" => typeof(CalendarioCitas),
                "ExportarPDF" => typeof(ExportarPDF),



                _ => null
            };

            if (destino == null)
                return;

            if (MainFrame.CurrentSourcePageType != destino)
                MainFrame.Navigate(destino);

            foreach (var elemento in NavView.MenuItems)
            {
                if (elemento is NavigationViewItem item &&
                    item.Tag?.ToString() == pantalla)
                {
                    NavView.SelectedItem = item;
                    break;
                }
            }
        }

        private void NavView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            if (MainFrame == null || args.IsSettingsSelected)
                return;

            if (args.SelectedItem is NavigationViewItem item)
            {
                string pantalla = item.Tag?.ToString() ?? "";
                SeleccionarPantalla(pantalla);
            }
        }
    }
}
