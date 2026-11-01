
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.UI;

namespace VeterinariaServerSurPia.Views
{
    public sealed class ClienteAdmin
    {
        public string ID { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Correo { get; set; } = "";
        public string TextoMostrar => $"{ID} - {Nombre}";
    }

    public sealed class RegistroAdmin
    {
        public string ID { get; set; } = "";
        public string[] Datos { get; set; } = Array.Empty<string>();
        public string Titulo { get; set; } = "";
        public string Detalle { get; set; } = "";
    }

    public sealed partial class ModificarEliminar : Page
    {
        private readonly List<ClienteAdmin> todosClientes = new();

        private string idClienteActual = "";
        private string idMascotaActual = "";
        private string idCitaActual = "";

        private string[] datosCitaActual = Array.Empty<string>();
        private bool cargandoClientes;

        private static readonly CultureInfo Cultura =
            CultureInfo.InvariantCulture;

        public ModificarEliminar()
        {
            InitializeComponent();
            CargarClientes();
            LimpiarMascota();
            LimpiarCita();
            CargarCitaPendiente();
        }

        private string Ruta(string archivo) =>
            Path.Combine(AppContext.BaseDirectory, archivo);

        private List<string[]> Leer(string archivo, int minimo)
        {
            string ruta = Ruta(archivo);

            if (!File.Exists(ruta))
                return new List<string[]>();

            return File.ReadAllLines(ruta)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(','))
                .Where(p => p.Length >= minimo)
                .ToList();
        }

        private static bool MismoId(string a, string b) =>
            a.Trim().Equals(
                b.Trim(),
                StringComparison.OrdinalIgnoreCase);

        private static bool ContieneComa(params string[] valores) =>
            valores.Any(v => v.Contains(','));

        private static bool LeerFecha(string texto, out DateTime fecha)
        {
            return DateTime.TryParseExact(
                texto.Trim(),
                new[]
                {
                    "dd/MM/yyyy", "d/M/yyyy",
                    "yyyy-MM-dd", "yyyy-M-d"
                },
                Cultura,
                DateTimeStyles.None,
                out fecha);
        }

        private static bool LeerHora(string texto, out TimeSpan hora)
        {
            if (TimeSpan.TryParseExact(
                texto.Trim(),
                new[] { @"hh\:mm", @"h\:mm" },
                Cultura,
                out hora))
            {
                return hora >= TimeSpan.Zero &&
                       hora < TimeSpan.FromDays(1);
            }

            if (DateTime.TryParseExact(
                texto.Trim(),
                new[] { "h:mm tt", "hh:mm tt" },
                CultureInfo.GetCultureInfo("en-US"),
                DateTimeStyles.None,
                out DateTime fechaHora))
            {
                hora = fechaHora.TimeOfDay;
                return true;
            }

            hora = TimeSpan.Zero;
            return false;
        }

        private bool ClienteInactivo(string id)
        {
            string ruta = Ruta("clientes_inactivos.txt");

            return File.Exists(ruta) &&
                File.ReadAllLines(ruta)
                    .Any(l => MismoId(l, id));
        }

        private void MostrarMensaje(string mensaje, bool error)
        {
            TxtMensaje.Text = mensaje;
            TxtMensaje.Foreground = new SolidColorBrush(
                error
                    ? Color.FromArgb(255, 255, 150, 150)
                    : Color.FromArgb(255, 158, 217, 181));
        }

        private async System.Threading.Tasks.Task<bool> Confirmar(
            string titulo, string mensaje)
        {
            ContentDialog dialogo = new()
            {
                Title = titulo,
                Content = mensaje,
                PrimaryButtonText = "Confirmar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            return await dialogo.ShowAsync() ==
                   ContentDialogResult.Primary;
        }

        private bool ReemplazarRegistro(
            string archivo, string id, string[] datos)
        {
            string ruta = Ruta(archivo);

            if (!File.Exists(ruta))
                return false;

            string[] lineas = File.ReadAllLines(ruta);

            int indice = Array.FindIndex(lineas, l =>
            {
                string[] p = l.Split(',');
                return p.Length > 0 && MismoId(p[0], id);
            });

            if (indice < 0)
                return false;

            lineas[indice] = string.Join(",", datos);
            File.WriteAllLines(ruta, lineas);
            return true;
        }

        private bool EliminarRegistro(string archivo, string id)
        {
            string ruta = Ruta(archivo);

            if (!File.Exists(ruta))
                return false;

            string[] lineas = File.ReadAllLines(ruta);

            string[] restantes = lineas.Where(l =>
            {
                string[] p = l.Split(',');
                return p.Length == 0 || !MismoId(p[0], id);
            }).ToArray();

            if (restantes.Length == lineas.Length)
                return false;

            File.WriteAllLines(ruta, restantes);
            return true;
        }

        private void CargarClientes(string conservarId = "")
        {
            cargandoClientes = true;
            todosClientes.Clear();

            foreach (string[] p in Leer("clientes.txt", 4))
            {
                todosClientes.Add(new ClienteAdmin
                {
                    ID = p[0].Trim(),
                    Nombre = p[1].Trim(),
                    Telefono = p[2].Trim(),
                    Correo = p[3].Trim()
                });
            }

            ActualizarListaClientes();

            if (!string.IsNullOrWhiteSpace(conservarId))
            {
                ClienteCombo.SelectedItem =
                    todosClientes.FirstOrDefault(c =>
                        MismoId(c.ID, conservarId));
            }

            cargandoClientes = false;
        }

        private void ActualizarListaClientes()
        {
            string filtro = TxtBuscarCliente.Text.Trim();

            var lista = todosClientes
                .Where(c =>
                    filtro.Length == 0 ||
                    c.ID.Contains(
                        filtro, StringComparison.OrdinalIgnoreCase) ||
                    c.Nombre.Contains(
                        filtro, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Nombre)
                .ToList();

            ClienteCombo.ItemsSource = null;
            ClienteCombo.ItemsSource = lista;

            TxtEstadoCliente.Text =
                $"Clientes encontrados: {lista.Count}";
        }

        private void TxtBuscarCliente_TextChanged(
            object sender, TextChangedEventArgs e)
        {
            if (cargandoClientes || ClienteCombo == null)
                return;

            string anterior = idClienteActual;
            cargandoClientes = true;
            ActualizarListaClientes();

            ClienteCombo.SelectedItem =
                (ClienteCombo.ItemsSource as IEnumerable<ClienteAdmin>)?
                .FirstOrDefault(c => MismoId(c.ID, anterior));

            cargandoClientes = false;

            if (ClienteCombo.SelectedItem == null)
                LimpiarCliente();
        }

        private void ClienteCombo_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (cargandoClientes)
                return;

            if (ClienteCombo.SelectedItem is ClienteAdmin cliente)
                CargarCliente(cliente.ID);
            else
                LimpiarCliente();
        }

        private void CargarCliente(string id)
        {
            string[]? p = Leer("clientes.txt", 4)
                .FirstOrDefault(x => MismoId(x[0], id));

            if (p == null)
            {
                MostrarMensaje("Cliente no encontrado.", true);
                return;
            }

            idClienteActual = p[0].Trim();

            TxtIDCliente.Text = p[0].Trim();
            TxtNombreCliente.Text = p[1].Trim();
            TxtTelefonoCliente.Text = p[2].Trim();
            TxtCorreoCliente.Text = p[3].Trim();

            bool inactivo = ClienteInactivo(idClienteActual);

            TxtEstadoBaja.Text = inactivo
                ? "Estado: cliente dado de baja."
                : "Estado: cliente activo.";

            BtnDarBajaCliente.Content = inactivo
                ? "Cliente dado de baja"
                : "Dar de baja cliente";

            BtnDarBajaCliente.IsEnabled = !inactivo;
            BtnEliminarCliente.IsEnabled = true;

            TxtEstadoCliente.Text =
                $"Cliente seleccionado: {p[1].Trim()}";

            CargarMascotas();
            CargarCitas();
        }

        private void LimpiarCliente()
        {
            idClienteActual = "";

            TxtIDCliente.Text = "";
            TxtNombreCliente.Text = "";
            TxtTelefonoCliente.Text = "";
            TxtCorreoCliente.Text = "";
            TxtEstadoBaja.Text = "";

            BtnDarBajaCliente.IsEnabled = false;
            BtnEliminarCliente.IsEnabled = false;

            MascotasListView.ItemsSource = null;
            CitasListView.ItemsSource = null;

            LimpiarMascota();
            LimpiarCita();
        }

        private void BtnGuardarCliente_Click(
            object sender, RoutedEventArgs e)
        {
            if (idClienteActual.Length == 0)
            {
                MostrarMensaje("Selecciona un cliente.", true);
                return;
            }

            string nombre = TxtNombreCliente.Text.Trim();
            string telefono = TxtTelefonoCliente.Text.Trim();
            string correo = TxtCorreoCliente.Text.Trim();

            if (nombre.Length == 0 ||
                telefono.Length == 0 ||
                correo.Length == 0)
            {
                MostrarMensaje(
                    "Completa nombre, teléfono y correo.", true);
                return;
            }

            if (!Regex.IsMatch(telefono, @"^\d{10}$"))
            {
                MostrarMensaje(
                    "El teléfono debe tener 10 dígitos.", true);
                return;
            }

            if (!Regex.IsMatch(
                correo,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                MostrarMensaje(
                    "Escribe un correo válido.", true);
                return;
            }

            if (ContieneComa(nombre, telefono, correo))
            {
                MostrarMensaje(
                    "No utilices comas en los campos.", true);
                return;
            }

            bool guardado = ReemplazarRegistro(
                "clientes.txt",
                idClienteActual,
                new[] { idClienteActual, nombre, telefono, correo });

            if (!guardado)
            {
                MostrarMensaje("No se encontró el cliente.", true);
                return;
            }

            string id = idClienteActual;
            CargarClientes(id);
            CargarCliente(id);

            MostrarMensaje(
                "Cliente actualizado correctamente.", false);
        }

        private async void BtnDarBajaCliente_Click(
            object sender, RoutedEventArgs e)
        {
            if (idClienteActual.Length == 0)
            {
                MostrarMensaje("Selecciona un cliente.", true);
                return;
            }

            if (ClienteInactivo(idClienteActual))
            {
                MostrarMensaje(
                    "El cliente ya está dado de baja.", true);
                return;
            }

            bool aceptar = await Confirmar(
                "Dar de baja cliente",
                "Se conservarán sus datos, mascotas y citas. " +
                "¿Deseas continuar?");

            if (!aceptar)
                return;

            File.AppendAllText(
                Ruta("clientes_inactivos.txt"),
                idClienteActual + Environment.NewLine);

            CargarCliente(idClienteActual);

            MostrarMensaje(
                "Cliente dado de baja correctamente.", false);
        }

        private async void BtnEliminarCliente_Click(
            object sender, RoutedEventArgs e)
        {
            if (idClienteActual.Length == 0)
            {
                MostrarMensaje("Selecciona un cliente.", true);
                return;
            }

            bool tieneMascotas = Leer("mascotas.txt", 8)
                .Any(p => MismoId(p[7], idClienteActual));

            if (tieneMascotas)
            {
                MostrarMensaje(
                    "No puedes eliminar este cliente porque " +
                    "tiene mascotas. Puedes darlo de baja.", true);
                return;
            }

            if (!await Confirmar(
                "Eliminar cliente",
                "¿Deseas eliminar definitivamente este cliente?"))
                return;

            string id = idClienteActual;

            if (!EliminarRegistro("clientes.txt", id))
            {
                MostrarMensaje("No se encontró el cliente.", true);
                return;
            }

            string rutaInactivos = Ruta("clientes_inactivos.txt");

            if (File.Exists(rutaInactivos))
            {
                File.WriteAllLines(
                    rutaInactivos,
                    File.ReadAllLines(rutaInactivos)
                        .Where(l => !MismoId(l, id)));
            }

            LimpiarCliente();
            CargarClientes();

            MostrarMensaje("Cliente eliminado.", false);
        }

        private void CargarMascotas(string conservarId = "")
        {
            var mascotas = Leer("mascotas.txt", 8)
                .Where(p => MismoId(p[7], idClienteActual))
                .Select(p => new RegistroAdmin
                {
                    ID = p[0].Trim(),
                    Datos = p,
                    Titulo = $"{p[1].Trim()} — ID {p[0].Trim()}",
                    Detalle =
                        $"{p[2].Trim()} · {p[3].Trim()} · " +
                        $"Peso: {p[6].Trim()} kg"
                })
                .ToList();

            MascotasListView.ItemsSource = null;
            MascotasListView.ItemsSource = mascotas;

            MascotasListView.SelectedItem =
                mascotas.FirstOrDefault(m =>
                    MismoId(m.ID, conservarId));

            if (MascotasListView.SelectedItem == null)
                LimpiarMascota();
        }

        private void MascotasListView_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (MascotasListView.SelectedItem is not RegistroAdmin m)
            {
                LimpiarMascota();
                return;
            }

            idMascotaActual = m.ID;

            TxtIDMascota.Text = m.Datos[0].Trim();
            TxtNombreMascota.Text = m.Datos[1].Trim();
            TxtEspecieMascota.Text = m.Datos[2].Trim();
            TxtRazaMascota.Text = m.Datos[3].Trim();
            TxtEdadMascota.Text = m.Datos[4].Trim();
            TxtSexoMascota.Text = m.Datos[5].Trim();
            TxtPesoMascota.Text = m.Datos[6].Trim();
        }

        private void LimpiarMascota()
        {
            idMascotaActual = "";

            TxtIDMascota.Text = "";
            TxtNombreMascota.Text = "";
            TxtEspecieMascota.Text = "";
            TxtRazaMascota.Text = "";
            TxtEdadMascota.Text = "";
            TxtSexoMascota.Text = "";
            TxtPesoMascota.Text = "";
        }

        private void BtnGuardarMascota_Click(
            object sender, RoutedEventArgs e)
        {
            if (idMascotaActual.Length == 0)
            {
                MostrarMensaje("Selecciona una mascota.", true);
                return;
            }

            string nombre = TxtNombreMascota.Text.Trim();
            string especie = TxtEspecieMascota.Text.Trim();
            string raza = TxtRazaMascota.Text.Trim();
            string edadTexto = TxtEdadMascota.Text.Trim();
            string sexo = TxtSexoMascota.Text.Trim();
            string pesoTexto = TxtPesoMascota.Text.Trim();

            if (new[]
            {
                nombre, especie, raza, edadTexto, sexo, pesoTexto
            }.Any(string.IsNullOrWhiteSpace))
            {
                MostrarMensaje(
                    "Completa todos los datos de la mascota.", true);
                return;
            }

            if (!int.TryParse(edadTexto, out int edad) ||
                edad < 0 || edad > 100)
            {
                MostrarMensaje(
                    "La edad debe ser un número entre 0 y 100.", true);
                return;
            }

            if (!decimal.TryParse(
                    pesoTexto,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal peso) &&
                !decimal.TryParse(
                    pesoTexto,
                    NumberStyles.Number,
                    Cultura,
                    out peso))
            {
                MostrarMensaje(
                    "El peso debe ser numérico.", true);
                return;
            }

            if (peso <= 0 || peso > 500)
            {
                MostrarMensaje(
                    "El peso debe ser mayor a 0 y máximo 500 kg.", true);
                return;
            }

            string pesoGuardado =
                peso.ToString(Cultura);

            string[] datos =
            {
                idMascotaActual,
                nombre,
                especie,
                raza,
                edad.ToString(),
                sexo,
                pesoGuardado,
                idClienteActual
            };

            if (ContieneComa(datos))
            {
                MostrarMensaje(
                    "No utilices comas en los campos.", true);
                return;
            }

            bool guardado = ReemplazarRegistro(
                "mascotas.txt", idMascotaActual, datos);

            if (!guardado)
            {
                MostrarMensaje(
                    "No se encontró la mascota.", true);
                return;
            }

            string id = idMascotaActual;
            CargarMascotas(id);
            CargarCitas();

            MostrarMensaje(
                "Mascota actualizada correctamente.", false);
        }

        private async void BtnEliminarMascota_Click(
            object sender, RoutedEventArgs e)
        {
            if (idMascotaActual.Length == 0)
            {
                MostrarMensaje("Selecciona una mascota.", true);
                return;
            }

            bool tieneCitas = Leer("citas.txt", 5)
                .Any(p => MismoId(p[1], idMascotaActual));

            if (tieneCitas)
            {
                MostrarMensaje(
                    "No puedes eliminar esta mascota porque " +
                    "tiene citas registradas.", true);
                return;
            }

            if (!await Confirmar(
                "Eliminar mascota",
                "¿Deseas eliminar esta mascota definitivamente?"))
                return;

            string id = idMascotaActual;

            if (!EliminarRegistro("mascotas.txt", id))
            {
                MostrarMensaje("No se encontró la mascota.", true);
                return;
            }

            CargarMascotas();
            CargarCitas();

            MostrarMensaje("Mascota eliminada.", false);
        }

        private void CargarCitas(string conservarId = "")
        {
            var mascotas = Leer("mascotas.txt", 8)
                .Where(p => MismoId(p[7], idClienteActual))
                .ToList();

            var ids = mascotas
                .Select(p => p[0].Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var nombres = mascotas
                .GroupBy(p => p[0].Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First()[1].Trim(),
                    StringComparer.OrdinalIgnoreCase);

            var citas = Leer("citas.txt", 5)
                .Where(p => ids.Contains(p[1].Trim()))
                .Select(p =>
                {
                    string idMascota = p[1].Trim();

                    string nombre = nombres.TryGetValue(
                        idMascota, out string? n)
                        ? n
                        : "Mascota no encontrada";

                    return new RegistroAdmin
                    {
                        ID = p[0].Trim(),
                        Datos = p,
                        Titulo = $"Cita {p[0].Trim()} — {nombre}",
                        Detalle =
                            $"Fecha: {p[2].Trim()} · " +
                            $"Hora: {p[3].Trim()} · " +
                            $"Servicio: {p[4].Trim()}"
                    };
                })
                .ToList();

            CitasListView.ItemsSource = null;
            CitasListView.ItemsSource = citas;

            CitasListView.SelectedItem =
                citas.FirstOrDefault(c =>
                    MismoId(c.ID, conservarId));

            if (CitasListView.SelectedItem == null)
                LimpiarCita();
        }

        private void CitasListView_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (CitasListView.SelectedItem is not RegistroAdmin cita)
            {
                LimpiarCita();
                return;
            }

            idCitaActual = cita.ID;
            datosCitaActual = cita.Datos;

            TxtIDCita.Text = cita.Datos[0].Trim();
            TxtMascotaCita.Text = cita.Datos[1].Trim();

            if (LeerFecha(cita.Datos[2], out DateTime fecha))
            {
                FechaCitaPicker.Date =
                    new DateTimeOffset(fecha);
            }
            else
            {
                FechaCitaPicker.Date = DateTimeOffset.Now;
            }

            TxtHoraCita.Text = cita.Datos[3].Trim();
            TxtServicioCita.Text = cita.Datos[4].Trim();

            TxtObservacionesCita.Text =
                cita.Datos.Length > 5
                    ? string.Join(",", cita.Datos.Skip(5))
                    : "";
        }

        private void LimpiarCita()
        {
            idCitaActual = "";
            datosCitaActual = Array.Empty<string>();

            TxtIDCita.Text = "";
            TxtMascotaCita.Text = "";
            FechaCitaPicker.Date = DateTimeOffset.Now;
            TxtHoraCita.Text = "";
            TxtServicioCita.Text = "";
            TxtObservacionesCita.Text = "";
        }

        private void BtnGuardarCita_Click(
            object sender, RoutedEventArgs e)
        {
            if (idCitaActual.Length == 0 ||
                datosCitaActual.Length < 5)
            {
                MostrarMensaje("Selecciona una cita.", true);
                return;
            }

            string horaTexto = TxtHoraCita.Text.Trim();
            string servicio = TxtServicioCita.Text.Trim();
            string observaciones =
                TxtObservacionesCita.Text.Trim();

            if (!LeerHora(horaTexto, out TimeSpan hora))
            {
                MostrarMensaje(
                    "Escribe una hora válida, por ejemplo 14:30.",
                    true);
                return;
            }

            if (servicio.Length == 0)
            {
                MostrarMensaje(
                    "El servicio es obligatorio.", true);
                return;
            }

            if (observaciones.Length > 500)
            {
                MostrarMensaje(
                    "Máximo 500 caracteres en observaciones.",
                    true);
                return;
            }

            if (ContieneComa(servicio, observaciones))
            {
                MostrarMensaje(
                    "No escribas comas en servicio u observaciones.",
                    true);
                return;
            }

            DateTime fecha = FechaCitaPicker.Date.Date;

            if (fecha.Add(hora) <= DateTime.Now)
            {
                MostrarMensaje(
                    "La nueva fecha y hora deben ser futuras.",
                    true);
                return;
            }

            bool horarioOcupado = Leer("citas.txt", 5)
                .Any(p =>
                {
                    if (MismoId(p[0], idCitaActual))
                        return false;

                    if (!LeerFecha(p[2], out DateTime otraFecha) ||
                        !LeerHora(p[3], out TimeSpan otraHora))
                        return false;

                    return otraFecha.Date == fecha.Date &&
                           otraHora == hora;
                });

            if (horarioOcupado)
            {
                MostrarMensaje(
                    "Ya existe otra cita en esa fecha y hora.",
                    true);
                return;
            }

            string[] datos =
            {
                idCitaActual,
                datosCitaActual[1].Trim(),
                fecha.ToString("dd/MM/yyyy", Cultura),
                hora.ToString(@"hh\:mm"),
                servicio,
                observaciones
            };

            if (!ReemplazarRegistro(
                "citas.txt", idCitaActual, datos))
            {
                MostrarMensaje("No se encontró la cita.", true);
                return;
            }

            string id = idCitaActual;
            CargarCitas(id);

            MostrarMensaje(
                "Cita actualizada correctamente.", false);
        }

        private async void BtnEliminarCita_Click(
            object sender, RoutedEventArgs e)
        {
            if (idCitaActual.Length == 0)
            {
                MostrarMensaje("Selecciona una cita.", true);
                return;
            }

            if (!await Confirmar(
                "Eliminar cita",
                $"¿Deseas eliminar la cita {idCitaActual}?"))
                return;

            string id = idCitaActual;

            if (!EliminarRegistro("citas.txt", id))
            {
                MostrarMensaje("No se encontró la cita.", true);
                return;
            }

            CargarCitas();

            MostrarMensaje("Cita eliminada.", false);
        }

        private void CargarCitaPendiente()
        {
            string idCita = MainWindow.CitaPendienteId;

            if (string.IsNullOrWhiteSpace(idCita))
                return;

            MainWindow.CitaPendienteId = "";

            string[]? cita = Leer("citas.txt", 5)
                .FirstOrDefault(p => MismoId(p[0], idCita));

            if (cita == null)
            {
                MostrarMensaje(
                    "No se encontró la cita seleccionada.", true);
                return;
            }

            string[]? mascota = Leer("mascotas.txt", 8)
                .FirstOrDefault(p =>
                    MismoId(p[0], cita[1]));

            if (mascota == null)
            {
                MostrarMensaje(
                    "No se encontró la mascota de esta cita.", true);
                return;
            }

            ClienteAdmin? cliente = todosClientes
                .FirstOrDefault(c =>
                    MismoId(c.ID, mascota[7]));

            if (cliente == null)
            {
                MostrarMensaje(
                    "No se encontró al propietario.", true);
                return;
            }

            cargandoClientes = true;
            TxtBuscarCliente.Text = "";
            ActualizarListaClientes();
            ClienteCombo.SelectedItem = cliente;
            cargandoClientes = false;

            CargarCliente(cliente.ID);
            CargarCitas(idCita);

            MostrarMensaje(
                $"Cita {idCita} cargada desde el calendario.",
                false);
        }

        private void BtnVerExpediente_Click(
            object sender, RoutedEventArgs e)
        {
            if (idMascotaActual.Length == 0)
            {
                MostrarMensaje(
                    "Selecciona una mascota primero.", true);
                return;
            }

            MainWindow.Current?.SeleccionarPantalla(
                "ConsultarHistorial");
        }

        private void BtnIrBusqueda_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla(
                "Busqueda");
        }
    }
}
