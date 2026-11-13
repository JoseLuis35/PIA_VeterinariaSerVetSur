
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using Windows.System;
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();

            var blanco = Colors.White;
            var naranja = ColorHelper.FromArgb(255, 242, 140, 40);

            CrearPuntos(PuntosIzqSup, 12, 8, 14, blanco, 0.55);
            CrearPuntos(PuntosIzqInf, 10, 7, 13, blanco, 0.45);
            CrearPuntos(PuntosDerSup, 13, 9, 14, naranja, 0.35);
            CrearPuntos(PuntosDerInf, 14, 7, 14, naranja, 0.35);
        }

        private void CrearPuntos(
            Canvas lienzo,
            int columnas,
            int filas,
            double separacion,
            Windows.UI.Color color,
            double opacidad)
        {
            for (int f = 0; f < filas; f++)
            {
                for (int c = 0; c < columnas; c++)
                {
                    var punto = new Ellipse
                    {
                        Width = 2.4,
                        Height = 2.4,
                        Fill = new SolidColorBrush(color),
                        Opacity = opacidad
                    };

                    Canvas.SetLeft(punto, c * separacion);
                    Canvas.SetTop(punto, f * separacion);
                    lienzo.Children.Add(punto);
                }
            }
        }

        private bool actualizandoPassword = false;

        private void TxtUsuario_BeforeTextChanging(
            TextBox sender,
            TextBoxBeforeTextChangingEventArgs args)
        {
            if (!UsuarioValido(args.NewText))
            {
                args.Cancel = true;

                MostrarError(
                    "El usuario solo puede contener letras y números, " +
                    "sin espacios ni símbolos.");
            }
            else
            {
                OcultarError();
            }
        }

        private void TxtPassword_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (actualizandoPassword)
            {
                return;
            }

            string actual = TxtPassword.Password;

            if (ContieneEspacios(actual))
            {
                actualizandoPassword = true;

                TxtPassword.Password = QuitarEspacios(actual);

                actualizandoPassword = false;

                MostrarError(
                    "La contraseña no puede contener espacios.");
            }
            else
            {
                OcultarError();
            }
        }

        private static bool UsuarioValido(string texto)
        {
            foreach (char c in texto)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContieneEspacios(string texto)
        {
            foreach (char c in texto)
            {
                if (char.IsWhiteSpace(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static string QuitarEspacios(string texto)
        {
            var sb = new System.Text.StringBuilder(texto.Length);

            foreach (char c in texto)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private void TxtUsuario_KeyDown(
            object sender,
            KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && !e.KeyStatus.WasKeyDown)
            {
                e.Handled = true;
                TxtPassword.Focus(FocusState.Programmatic);
            }
        }

        private void TxtPassword_KeyDown(
            object sender,
            KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter && !e.KeyStatus.WasKeyDown)
            {
                e.Handled = true;

                BtnLogin_Click(
                    BtnLogin,
                    new RoutedEventArgs());
            }
        }

        private void BtnLogin_Click(
            object sender,
            RoutedEventArgs e)
        {
            string usuario = TxtUsuario.Text.Trim();
            string password = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(usuario) ||
                string.IsNullOrWhiteSpace(password))
            {
                MostrarError(
                    "Por favor, ingrese usuario y contraseña.");

                if (string.IsNullOrWhiteSpace(usuario))
                {
                    TxtUsuario.Focus(FocusState.Programmatic);
                }
                else
                {
                    TxtPassword.Focus(FocusState.Programmatic);
                }

                return;
            }

            if (!UsuarioValido(usuario))
            {
                MostrarError(
                    "El usuario solo puede contener letras y números, " +
                    "sin espacios ni símbolos.");

                TxtUsuario.Focus(FocusState.Programmatic);
                return;
            }

            if (ContieneEspacios(password))
            {
                MostrarError(
                    "La contraseña no puede contener espacios.");

                TxtPassword.Focus(FocusState.Programmatic);
                return;
            }

            string rutaArchivo = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "usuarios.txt");

            try
            {
                if (!File.Exists(rutaArchivo))
                {
                    File.WriteAllLines(
                        rutaArchivo,
                        new[]
                        {
                            "Veterinario,979797",
                            "empleado,secreto"
                        });
                }
            }
            catch
            {
            }

            bool credencialesValidas = false;

            try
            {
                if (File.Exists(rutaArchivo))
                {
                    string[] lineas = File.ReadAllLines(rutaArchivo);

                    foreach (string linea in lineas)
                    {
                        string[] datos = linea.Split(',');

                        if (datos.Length >= 2 &&
                            datos[0].Trim() == usuario &&
                            datos[1].Trim() == password)
                        {
                            credencialesValidas = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                MostrarError(
                    "No fue posible leer el archivo de usuarios.");

                return;
            }

            if (credencialesValidas)
            {
                MainWindow.Current.MostrarMenuPrincipal();
            }
            else
            {
                MostrarError(
                    "Usuario o contraseña incorrectos.");

                TxtPassword.Password = string.Empty;
                TxtPassword.Focus(FocusState.Programmatic);
            }
        }

        private void OcultarError()
        {
            PanelError.Visibility = Visibility.Collapsed;
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
        }
    }
}
