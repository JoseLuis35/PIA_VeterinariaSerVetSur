using Microsoft.UI.Xaml.Controls;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class MenuPage : Page
    {
        public MenuPage()
        {
            this.InitializeComponent();
        }

        private void NavMenu_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            // Aquí se conectarán las páginas de tus compañeros en ContentFrame
            this.Frame.Navigate(typeof(VeterinariaServerSurPia.Views.BusquedaConsulta));
        }
    }
}