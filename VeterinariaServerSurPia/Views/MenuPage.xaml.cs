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
            this.Frame.Navigate(typeof(VeterinariaServerSurPia.Views.BusquedaConsulta));
        }
    }
}