
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VeterinariaServerSurPia.Services;

namespace VeterinariaServerSurPia
{
    public partial class App : Application
    {
        private Window? _window;

        public static DatabaseService Database { get; private set; } = null!;

        public App()
        {
            InitializeComponent();

            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "es";
        }

        protected override async void OnLaunched(
            Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Database = new DatabaseService();

            try
            {
                await Database.InitializeAsync();

                Debug.WriteLine(
                    "VetSmart AI 2.0: SQLite inicializado correctamente."
                );

                Debug.WriteLine(
                    "Ubicación de la base de datos: " +
                    Database.GetDatabasePath()
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Error al inicializar SQLite: " + ex.Message
                );

                Debug.WriteLine(ex.ToString());
            }

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
