using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VeterinariaServerSurPia.Services;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class AsistenteView : Page
    {

        private sealed class MascotaOpcion
        {
            public string Id { get; set; } = "";
            public string Nombre { get; set; } = "";
            public string Especie { get; set; } = "";
            public string Raza { get; set; } = "";
            public string Edad { get; set; } = "";
            public string Sexo { get; set; } = "";
            public string Peso { get; set; } = "";
            public string IdPropietario { get; set; } = "";

            public string TextoMostrar => $"{Nombre} ({Id})";
        }

        private sealed class RegistroConocimiento
        {
            public string IdMascota { get; set; } = "";
            public string NombreMascota { get; set; } = "";
            public string Especie { get; set; } = "";
            public DateTime Fecha { get; set; }
            public string Observacion { get; set; } = "";
            public string Diagnostico { get; set; } = "";
            public string Fuente { get; set; } = "";
            public string Raza { get; set; } = "";
            public string Edad { get; set; } = "";
            public string Sexo { get; set; } = "";
            public string Tratamiento { get; set; } = "";
            public string Alergias { get; set; } = "";
        }

        private sealed class InterpretacionClinica
        {
            public HashSet<string> Presentes { get; set; } = new();
            public HashSet<string> Ausentes { get; set; } = new();
            public HashSet<string> Inciertos { get; set; } = new();
        }

        private sealed class CasoRelacionado
        {
            public RegistroConocimiento Registro { get; set; } = new();
            public double Similitud { get; set; }
            public List<string> Coincidencias { get; set; } = new();

            public double Afinidad { get; set; }
            public List<string> Afinidades { get; set; } = new();

            public bool MismaEspecie { get; set; }
        }


        private readonly List<MascotaOpcion> mascotas = new();

        private readonly OllamaService ollamaService = new();

        private MascotaOpcion? mascotaSeleccionada;

        private readonly HashSet<string> imagenesFallidas = new();

        private bool aplicandoFiltroTexto = false;
        private bool cargandoMascotas = false;
        private bool consultandoOllama = false;
        private bool guardandoObservacion = false;
        private bool buscandoSimilares = false;

        private static readonly TimeSpan HoraApertura =
            new(8, 0, 0);

        private static readonly TimeSpan HoraCierre =
            new(17, 0, 0);

        private static readonly TimeSpan DuracionCita =
            TimeSpan.FromMinutes(30);


        private static readonly Dictionary<string, string> Patrones =
            new()
            {
                {
                    "apetito_bajo",
                    @"\b(no quiere comer|no tiene ganas de comer|no come|sin apetito|falta de apetito|perdida de apetito|inapetencia)\b"
                },
                {
                    "vomito",
                    @"\b(ha vomitado|esta vomitando|vomitos|vomito|vomita|vomitando|vomitar|emesis)\b"
                },
                {
                    "diarrea",
                    @"\b(evacuaciones liquidas|heces liquidas|deposiciones liquidas|diarreas|diarreico|diarrea)\b"
                },
                {
                    "fiebre",
                    @"\b(temperatura elevada|temperatura alta|febril|fiebre)\b"
                },
                {
                    "decaimiento",
                    @"\b(decaimiento|decaido|sin energia|letargo|letargico)\b"
                },
                {
                    "dificultad_respiratoria",
                    @"\b(dificultad para respirar|problemas para respirar|respira con dificultad|disnea)\b"
                },
                {
                    "tos",
                    @"\b(tosiendo|tos persistente|tos)\b"
                },
                {
                    "estornudo",
                    @"\b(estornudos|estornuda|estornudando|estornudo)\b"
                },
                {
                    "picazon",
                    @"\b(se rasca mucho|rascado excesivo|comezon|prurito|picazon)\b"
                },
                {
                    "cojera",
                    @"\b(cojera|cojea|cojeando|dificultad para caminar)\b"
                },
                {
                    "sangrado",
                    @"\b(sangrado|sangra|hemorragia)\b"
                },
                {
                    "desmayo",
                    @"\b(desmayos|desmayo|perdida de conciencia)\b"
                },
                {
                    "convulsion",
                    @"\b(convulsiones|convulsion|convulsionando)\b"
                }
            };


        public AsistenteView()
        {
            InitializeComponent();

            Loaded += AsistenteView_Loaded;
        }


        private async void AsistenteView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= AsistenteView_Loaded;

            _ = ollamaService.PrecalentarModeloAsync();

            await CargarMascotasAsync();
        }


        private static bool EsCaracterPermitido(char caracter)
        {
            if (char.IsLetterOrDigit(caracter))
                return true;

            return caracter switch
            {
                ' ' or '.' or ',' or '?' or '¿' or ':' or '/' or '-' => true,
                _ => false
            };
        }

        private void TextoLibre_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (aplicandoFiltroTexto)
                return;

            string textoOriginal = sender.Text;

            if (string.IsNullOrEmpty(textoOriginal))
                return;

            int posicionOriginal = sender.SelectionStart;

            StringBuilder textoCorregido = new();

            int nuevaPosicion = 0;

            for (int i = 0; i < textoOriginal.Length; i++)
            {
                char caracter = textoOriginal[i];

                if (caracter == '\r' || caracter == '\n')
                {
                    if (sender.AcceptsReturn)
                    {
                        if (textoCorregido.Length > 0 &&
                            textoCorregido[^1] != '\n')
                        {
                            while (textoCorregido.Length > 0 &&
                                   textoCorregido[^1] == ' ')
                            {
                                textoCorregido.Length--;
                            }

                            textoCorregido.Append('\n');
                        }
                    }
                }
                else
                {
                    if (char.IsWhiteSpace(caracter))
                        caracter = ' ';

                    if (char.IsControl(caracter) ||
                        !EsCaracterPermitido(caracter))
                    {
                        if (i < posicionOriginal)
                            nuevaPosicion = textoCorregido.Length;

                        continue;
                    }

                    if (caracter == ' ')
                    {
                        if (textoCorregido.Length == 0 ||
                            textoCorregido[^1] == '\n')
                        {
                            if (i < posicionOriginal)
                                nuevaPosicion = textoCorregido.Length;

                            continue;
                        }

                        if (textoCorregido[^1] == ' ')
                        {
                            if (i < posicionOriginal)
                                nuevaPosicion = textoCorregido.Length;

                            continue;
                        }

                        textoCorregido.Append(' ');
                    }
                    else
                    {
                        textoCorregido.Append(caracter);
                    }
                }

                if (i < posicionOriginal)
                    nuevaPosicion = textoCorregido.Length;
            }

            if (posicionOriginal >= textoOriginal.Length)
                nuevaPosicion = textoCorregido.Length;

            string resultado = textoCorregido.ToString();

            if (resultado == textoOriginal)
                return;

            nuevaPosicion = Math.Clamp(
                nuevaPosicion,
                0,
                resultado.Length);

            aplicandoFiltroTexto = true;

            try
            {
                sender.Text = resultado;
                sender.SelectionStart = nuevaPosicion;
                sender.SelectionLength = 0;
            }
            finally
            {
                aplicandoFiltroTexto = false;
            }
        }

        private static string NormalizarEspacios(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "";

            string[] lineas = texto
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            return string.Join(
                "\n",
                lineas.Select(linea =>
                    Regex.Replace(linea.Trim(), @"\s+", " ")))
                .Trim();
        }


        private void Imagen_ImageFailed(
            object sender,
            ExceptionRoutedEventArgs e)
        {
            string nombre =
                (sender as FrameworkElement)?.Tag as string ?? "imagen";

            RegistrarImagenFallida(nombre);
        }

        private void FondoEncabezado_ImageFailed(
            object sender,
            ExceptionRoutedEventArgs e)
        {
            RegistrarImagenFallida("FONDO_ENCABEZADO.png");
        }

        private void RegistrarImagenFallida(string nombre)
        {
            imagenesFallidas.Add(nombre);

            TxtAvisoImagenes.Text =
                "No se pudieron cargar estas imágenes de la carpeta Assets: " +
                string.Join(", ", imagenesFallidas.OrderBy(n => n)) + ".\n" +
                "Revisa que el nombre sea exacto y que cada archivo tenga " +
                "Acción de compilación = Contenido y " +
                "Copiar en el directorio de salida = Copiar si es posterior.";

            PanelAvisoImagenes.Visibility = Visibility.Visible;
        }


        private async Task CargarMascotasAsync()
        {
            if (cargandoMascotas)
                return;

            cargandoMascotas = true;

            string idAnterior = mascotaSeleccionada?.Id ?? "";

            try
            {
                mascotaSeleccionada = null;
                mascotas.Clear();

                CmbMascotas.ItemsSource = null;

                PanelCasosSimilares.Children.Clear();

                TxtDatosMascota.Text =
                    "Selecciona una mascota para consultar sus datos.";

                MostrarMensajeHistorial(
                    "Selecciona una mascota para consultar su historial.");

                TxtCasosSimilares.Text =
                    "Aquí aparecerán los antecedentes relacionados.";

                TxtMensajeMascotas.Text =
                    "Consultando mascotas registradas en SQLite...";

                if (App.Database == null)
                {
                    throw new InvalidOperationException(
                        "La base de datos no está inicializada.");
                }

                var registros =
                    await App.Database.ObtenerMascotasAsync();

                foreach (var registro in registros)
                {
                    mascotas.Add(new MascotaOpcion
                    {
                        Id = registro.Id,
                        Nombre = registro.Nombre,
                        Especie = registro.Especie,
                        Raza = registro.Raza,
                        Edad = registro.Edad,
                        Sexo = registro.Sexo,
                        Peso = registro.Peso,
                        IdPropietario = registro.IdPropietario
                    });
                }

                var ordenadas = mascotas
                    .OrderBy(m => m.Nombre)
                    .ToList();

                CmbMascotas.ItemsSource = ordenadas;

                TxtMensajeMascotas.Text =
                    $"Mascotas disponibles en SQLite: {ordenadas.Count}.";

                var anterior = ordenadas.FirstOrDefault(m =>
                    m.Id.Equals(
                        idAnterior,
                        StringComparison.OrdinalIgnoreCase));

                if (anterior != null)
                {
                    CmbMascotas.SelectedItem = anterior;
                }
            }
            catch (Exception ex)
            {
                TxtMensajeMascotas.Text =
                    "Error al consultar mascotas en SQLite: " +
                    ex.Message;
            }
            finally
            {
                cargandoMascotas = false;
            }
        }

        private async void BtnActualizarMascotas_Click(
            object sender,
            RoutedEventArgs e)
        {
            await CargarMascotasAsync();
        }


        private async void CmbMascotas_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            mascotaSeleccionada =
                CmbMascotas.SelectedItem as MascotaOpcion;

            TxtMensajeGuardar.Text = "";

            PanelCasosSimilares.Children.Clear();

            TxtCasosSimilares.Text =
                "Aquí aparecerán los antecedentes relacionados.";

            if (mascotaSeleccionada == null)
            {
                TxtDatosMascota.Text =
                    "Selecciona una mascota para consultar sus datos.";

                MostrarMensajeHistorial(
                    "Selecciona una mascota para consultar su historial.");

                return;
            }

            var m = mascotaSeleccionada;

            TxtDatosMascota.Text =
                $"ID: {m.Id}\n" +
                $"Nombre: {m.Nombre}\n" +
                $"Especie: {m.Especie}\n" +
                $"Raza: {m.Raza}\n" +
                $"Edad: {m.Edad}\n" +
                $"Sexo: {m.Sexo}\n" +
                $"Peso: {m.Peso} kg\n" +
                $"ID del propietario: {m.IdPropietario}";

            TxtObservacion.Text = "";
            TxtDiagnostico.Text = "";
            TxtConsultaIA.Text = "";

            await MostrarHistorialAsync();
        }


        private static string NormalizarPregunta(string texto)
        {
            string normalizado = texto
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            StringBuilder resultado = new();

            foreach (char c in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    resultado.Append(c);
                }
            }

            return resultado.ToString()
                .Normalize(NormalizationForm.FormC);
        }

        private MascotaOpcion? BuscarMascotaEnPregunta(string pregunta)
        {
            string preguntaNormalizada =
                NormalizarPregunta(pregunta);

            foreach (var mascota in mascotas)
            {
                string id = NormalizarPregunta(mascota.Id);

                if (!string.IsNullOrWhiteSpace(id) &&
                    Regex.IsMatch(
                        preguntaNormalizada,
                        $@"(?<![a-z0-9]){Regex.Escape(id)}(?![a-z0-9])"))
                {
                    return mascota;
                }
            }

            foreach (var mascota in mascotas
                .OrderByDescending(m => m.Nombre.Length))
            {
                string nombre = NormalizarPregunta(mascota.Nombre);

                if (string.IsNullOrWhiteSpace(nombre))
                    continue;

                if (Regex.IsMatch(
                    preguntaNormalizada,
                    $@"(?<![a-z0-9]){Regex.Escape(nombre)}(?![a-z0-9])"))
                {
                    return mascota;
                }
            }

            return null;
        }


        private static bool EsConsultaDeDisponibilidad(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(disponibilidad|disponibles|horarios libres|horario libre|espacios libres|espacio libre|huecos|citas libres|agenda libre|hay lugar|hay lugares|hay espacio|hay espacios|cuando puedo agendar|puedo agendar|puedo sacar una cita|agendar una cita|agendar cita|reservar una cita|reservar cita|sacar una cita)\b");
        }

        private static bool EsConsultaDeCitasRegistradas(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(citas registradas|citas agendadas|citas pendientes|proximas citas|proxima cita|proximas consultas|proxima consulta|ver las citas|mostrar las citas|consultar las citas|que citas hay|que citas tenemos|citas de hoy|citas de manana|agenda de hoy|agenda de manana|agenda registrada|citas programadas|consultas programadas)\b");
        }

        private static bool EsConsultaDeCartilla(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(cartilla|vacuna|vacunas|vacunacion|desparasitacion|desparasitaciones|proxima aplicacion|proximas aplicaciones|refuerzo)\b");
        }

        private static bool EsConsultaDeListadoMascotas(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(lista|listado|registradas|registrados|cuantas mascotas|que mascotas|cuales mascotas|mascotas tenemos)\b");
        }

        private static bool EsConsultaDeResumenGeneral(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(resumen general|resumen de la veterinaria|estado de la veterinaria|estadisticas generales|cuantos clientes|cuantas mascotas hay|informacion general)\b");
        }

        private static bool PareceConsultaDeDatos(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return EsConsultaDeCitasRegistradas(pregunta) ||
                   EsConsultaDeCartilla(pregunta) ||
                   EsConsultaDeListadoMascotas(pregunta) ||
                   EsConsultaDeResumenGeneral(pregunta) ||
                   Regex.IsMatch(
                       texto,
                       @"\b(historial|antecedentes|diagnostico registrado|tratamiento registrado|propietario|telefono|correo|peso registrado|datos de|informacion de|expediente|medicamento|medicamentos|dosis|ultima consulta)\b");
        }

        private static bool SolicitaInterpretacion(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(por que|porque|que significa|que puede significar|explica|explicame|recomiendas|que podria tener|que enfermedad|posibles causas|como puedo ayudar|que debo hacer|interpreta|analiza)\b");
        }


        private static DateTime? ExtraerFechaPregunta(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            DateTime hoy = DateTime.Today;

            if (Regex.IsMatch(texto, @"\b(pasado manana)\b"))
                return hoy.AddDays(2);

            if (Regex.IsMatch(texto, @"\b(manana)\b"))
                return hoy.AddDays(1);

            if (Regex.IsMatch(texto, @"\b(hoy)\b"))
                return hoy;

            Match fechaDiaMes = Regex.Match(
                texto,
                @"\b(?<dia>\d{1,2})[/-](?<mes>\d{1,2})[/-](?<anio>\d{4})\b");

            if (fechaDiaMes.Success)
            {
                if (int.TryParse(fechaDiaMes.Groups["dia"].Value, out int dia) &&
                    int.TryParse(fechaDiaMes.Groups["mes"].Value, out int mes) &&
                    int.TryParse(fechaDiaMes.Groups["anio"].Value, out int anio))
                {
                    if (DateTime.TryParseExact(
                        $"{dia:D2}/{mes:D2}/{anio}",
                        "dd/MM/yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime fecha))
                    {
                        return fecha.Date;
                    }
                }
            }

            Match fechaIso = Regex.Match(
                texto,
                @"\b(?<anio>\d{4})-(?<mes>\d{1,2})-(?<dia>\d{1,2})\b");

            if (fechaIso.Success)
            {
                if (int.TryParse(fechaIso.Groups["anio"].Value, out int anio) &&
                    int.TryParse(fechaIso.Groups["mes"].Value, out int mes) &&
                    int.TryParse(fechaIso.Groups["dia"].Value, out int dia))
                {
                    if (DateTime.TryParseExact(
                        $"{anio:D4}-{mes:D2}-{dia:D2}",
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime fecha))
                    {
                        return fecha.Date;
                    }
                }
            }

            Match fechaTexto = Regex.Match(
                texto,
                @"\b(?<dia>\d{1,2})\s+de\s+(?<mes>[a-z]+)(?:\s+de\s+(?<anio>\d{4}))?\b");

            if (fechaTexto.Success)
            {
                if (int.TryParse(fechaTexto.Groups["dia"].Value, out int dia))
                {
                    string nombreMes = fechaTexto.Groups["mes"].Value;

                    var meses = new Dictionary<string, int>
                    {
                        ["enero"] = 1,
                        ["febrero"] = 2,
                        ["marzo"] = 3,
                        ["abril"] = 4,
                        ["mayo"] = 5,
                        ["junio"] = 6,
                        ["julio"] = 7,
                        ["agosto"] = 8,
                        ["septiembre"] = 9,
                        ["setiembre"] = 9,
                        ["octubre"] = 10,
                        ["noviembre"] = 11,
                        ["diciembre"] = 12
                    };

                    if (meses.TryGetValue(nombreMes, out int mes))
                    {
                        int anio = fechaTexto.Groups["anio"].Success
                            ? int.Parse(fechaTexto.Groups["anio"].Value)
                            : hoy.Year;

                        if (!fechaTexto.Groups["anio"].Success &&
                            mes < hoy.Month)
                        {
                            anio++;
                        }

                        try
                        {
                            return new DateTime(anio, mes, dia);
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
            }

            return null;
        }


        private async Task<string> ObtenerDisponibilidadAsync(DateTime fecha)
        {
            if (App.Database == null)
            {
                throw new InvalidOperationException(
                    "La base de datos no está inicializada.");
            }

            if (fecha.Date < DateTime.Today)
            {
                return "No puedo consultar disponibilidad para una fecha pasada.";
            }

            using SqliteConnection connection =
                await App.Database.OpenConnectionAsync();

            if (connection.State != System.Data.ConnectionState.Open)
            {
                throw new InvalidOperationException(
                    "No fue posible abrir la conexión con SQLite.");
            }

            List<TimeSpan> horasOcupadas = new();

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Hora
                    FROM Citas
                    WHERE date(Fecha) = date($fecha)
                      AND Hora IS NOT NULL
                      AND TRIM(Hora) <> '';
                ";

                command.Parameters.AddWithValue(
                    "$fecha",
                    fecha.ToString("yyyy-MM-dd"));

                using SqliteDataReader reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string horaTexto = reader.IsDBNull(0)
                        ? ""
                        : reader.GetString(0).Trim();

                    if (IntentarConvertirHora(horaTexto, out TimeSpan hora))
                    {
                        horasOcupadas.Add(hora);
                    }
                }
            }

            List<TimeSpan> horariosDisponibles = new();

            for (
                TimeSpan hora = HoraApertura;
                hora + DuracionCita <= HoraCierre;
                hora += DuracionCita)
            {
                TimeSpan finPropuesto = hora + DuracionCita;

                bool ocupado = horasOcupadas.Any(horaOcupada =>
                {
                    TimeSpan finOcupado =
                        horaOcupada + DuracionCita;

                    return hora < finOcupado &&
                           finPropuesto > horaOcupada;
                });

                if (!ocupado)
                {
                    horariosDisponibles.Add(hora);
                }
            }

            StringBuilder resultado = new();

            resultado.AppendLine("DISPONIBILIDAD DE CITAS");
            resultado.AppendLine(
                $"Fecha consultada: {fecha:dd/MM/yyyy}");

            resultado.AppendLine(
                $"Horario de atención: {HoraApertura:hh\\:mm} a {HoraCierre:hh\\:mm}");

            resultado.AppendLine(
                $"Duración de cada cita: {(int)DuracionCita.TotalMinutes} minutos");

            resultado.AppendLine();

            if (horasOcupadas.Count > 0)
            {
                resultado.AppendLine("HORARIOS OCUPADOS:");

                foreach (TimeSpan hora in horasOcupadas.OrderBy(h => h))
                {
                    resultado.AppendLine(
                        $"- {hora:hh\\:mm} a {(hora + DuracionCita):hh\\:mm}");
                }

                resultado.AppendLine();
            }
            else
            {
                resultado.AppendLine(
                    "No hay citas registradas para esta fecha.");

                resultado.AppendLine();
            }

            if (horariosDisponibles.Count == 0)
            {
                resultado.AppendLine(
                    "No hay horarios disponibles para esta fecha.");
            }
            else
            {
                resultado.AppendLine("HORARIOS DISPONIBLES:");

                foreach (TimeSpan hora in horariosDisponibles)
                {
                    resultado.AppendLine(
                        $"- {hora:hh\\:mm} a {(hora + DuracionCita):hh\\:mm}");
                }

                resultado.AppendLine();

                resultado.AppendLine(
                    $"Total de horarios disponibles: {horariosDisponibles.Count}.");
            }

            resultado.AppendLine();
            resultado.AppendLine(
                "La disponibilidad se calculó con las citas registradas en SQLite.");

            resultado.AppendLine(
                "Estos horarios son informativos; la cita todavía no está reservada.");

            return resultado.ToString();
        }

        private static bool IntentarConvertirHora(
            string texto,
            out TimeSpan hora)
        {
            hora = TimeSpan.Zero;

            string[] formatos =
            {
                @"h\:mm",
                @"hh\:mm",
                @"h\:mm\:ss",
                @"hh\:mm\:ss"
            };

            if (TimeSpan.TryParseExact(
                texto,
                formatos,
                CultureInfo.InvariantCulture,
                out hora))
            {
                return true;
            }

            string[] formatosFecha =
            {
                "h:mm tt",
                "hh:mm tt",
                "h:mm:ss tt",
                "hh:mm:ss tt",
                "H:mm",
                "HH:mm",
                "H:mm:ss",
                "HH:mm:ss"
            };

            if (DateTime.TryParseExact(
                texto,
                formatosFecha,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime fechaHora))
            {
                hora = fechaHora.TimeOfDay;
                return true;
            }

            if (DateTime.TryParse(
                texto,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out fechaHora))
            {
                hora = fechaHora.TimeOfDay;
                return true;
            }

            return false;
        }


        private void MostrarRespuestaParcial(string texto)
        {
            TxtRespuestaOllama.Text = texto;
        }

        private async Task<string?> ResponderDatoDirectoAsync(
            string pregunta,
            string idMascota)
        {
            if (App.Database == null)
                return null;

            string n = NormalizarTexto(pregunta);

            string[] clinicas =
            {
                "diagnostic", "sintoma", "tratamiento", "enfermedad",
                "recomiend", "debo", "deberia", "que hago", "que hacer",
                "por que", "porque", "normal", "adecuad", "ideal",
                "sobrepeso", "riesgo", "dosis", "medicament", "vacun",
                "cita", "historial", "alergi", "compar", "cuidado",
                "aliment", "dieta", "podria", "puede", "significa",
                "interpret", "evalu", "sano", "salud", "grave", "urgen"
            };

            if (clinicas.Any(n.Contains))
                return null;

            if (n.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 12)
                return null;

            bool pideRaza = Regex.IsMatch(n, @"\braza\b");
            bool pideEspecie = Regex.IsMatch(n, @"\bespecie\b");
            bool pideEdad = Regex.IsMatch(n, @"\bedad\b|\bcuantos (anos|meses)\b");
            bool pideSexo = Regex.IsMatch(n, @"\bsexo\b|\b(macho|hembra)\b");
            bool pidePeso = Regex.IsMatch(n, @"\bpeso\b|\bpesa\b");
            bool pideDueno = Regex.IsMatch(n, @"\b(propietario|dueno|duena)\b");

            if (!pideRaza && !pideEspecie && !pideEdad &&
                !pideSexo && !pidePeso && !pideDueno)
            {
                return null;
            }

            var todas = await App.Database.ObtenerMascotasAsync();

            var mascota = todas.FirstOrDefault(m =>
                m.Id.Equals(idMascota, StringComparison.OrdinalIgnoreCase));

            if (mascota == null)
                return null;

            static string Dato(string valor) =>
                string.IsNullOrWhiteSpace(valor) ||
                valor.Equals("No registrado", StringComparison.OrdinalIgnoreCase)
                    ? "No registrado"
                    : valor.Trim();

            var lineas = new List<string>
            {
                $"{mascota.Nombre} ({mascota.Id})"
            };

            if (pideRaza)
                lineas.Add("• Raza: " + Dato(mascota.Raza));

            if (pideEspecie)
                lineas.Add("• Especie: " + Dato(mascota.Especie));

            if (pideEdad)
                lineas.Add("• Edad: " + Dato(mascota.Edad));

            if (pideSexo)
                lineas.Add("• Sexo: " + Dato(mascota.Sexo));

            if (pidePeso)
            {
                string peso = Dato(mascota.Peso);

                if (Regex.IsMatch(peso, @"^[\d.,]+$"))
                    peso += " kg";

                lineas.Add("• Peso: " + peso);
            }

            if (pideDueno)
            {
                string dueno = await App.Database.ObtenerNombreClienteAsync(
                    mascota.IdPropietario);

                lineas.Add("• Propietario: " + Dato(dueno) +
                    $" ({Dato(mascota.IdPropietario)})");
            }

            return string.Join("\n", lineas);
        }

        private async void BtnConsultarOllama_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (consultandoOllama)
                return;

            string pregunta =
                NormalizarEspacios(TxtPreguntaOllama.Text);

            if (string.IsNullOrWhiteSpace(pregunta))
            {
                TxtEstadoOllama.Text =
                    "Escribe una pregunta antes de consultar.";

                TxtPreguntaOllama.Focus(
                    Microsoft.UI.Xaml.FocusState.Programmatic);

                return;
            }

            if (pregunta.Length > 2000)
            {
                TxtEstadoOllama.Text =
                    "La pregunta no puede superar los 2000 caracteres.";

                return;
            }

            consultandoOllama = true;

            BtnConsultarOllama.IsEnabled = false;

            TxtEstadoOllama.Text =
                "Analizando la pregunta y consultando los registros...";

            TxtRespuestaOllama.Text =
                "VetSmart AI está procesando tu consulta...";

            try
            {
                if (App.Database == null)
                {
                    throw new InvalidOperationException(
                        "La base de datos no está inicializada.");
                }

                var consultaService =
                    new ConsultaVeterinariaService(App.Database);

                MascotaOpcion? mascotaMencionada =
                    BuscarMascotaEnPregunta(pregunta);

                string idMascota =
                    mascotaSeleccionada?.Id ?? "";

                if (string.IsNullOrWhiteSpace(idMascota) &&
                    mascotaMencionada != null)
                {
                    idMascota = mascotaMencionada.Id;
                }

                bool consultaGeneral =
                    string.IsNullOrWhiteSpace(idMascota);


                if (EsConsultaDeDisponibilidad(pregunta))
                {
                    DateTime? fecha = ExtraerFechaPregunta(pregunta);

                    if (fecha == null)
                    {
                        TxtRespuestaOllama.Text =
                            "Puedo consultar los horarios libres de la veterinaria.\n\n" +
                            "Nuestro horario de atención es de 8:00 a. m. a 5:00 p. m. " +
                            "y cada cita dura aproximadamente 30 minutos.\n\n" +
                            "¿Para qué fecha deseas consultar la disponibilidad? " +
                            "Puedes escribir, por ejemplo: 08/11/2026.";

                        TxtEstadoOllama.Text =
                            "Se necesita una fecha para consultar los horarios disponibles.";

                        return;
                    }

                    string disponibilidad =
                        await ObtenerDisponibilidadAsync(fecha.Value);

                    TxtRespuestaOllama.Text = disponibilidad;

                    TxtEstadoOllama.Text =
                        "Disponibilidad calculada con las citas registradas en SQLite.";

                    return;
                }


                if (EsConsultaDeCitasRegistradas(pregunta) &&
                    !SolicitaInterpretacion(pregunta))
                {
                    string resultado =
                        await consultaService.ObtenerProximasCitasAsync(30);

                    string contexto =
                        "Los siguientes datos son citas registradas en SQLite. " +
                        "No los confundas con horarios libres ni inventes reservaciones.\n\n" +
                        resultado;

                    string respuesta =
                        await ollamaService.GenerarRespuestaAsync(
                            contexto,
                            pregunta,
                            MostrarRespuestaParcial);

                    TxtRespuestaOllama.Text = respuesta;

                    TxtEstadoOllama.Text =
                        "Citas registradas consultadas desde SQLite e interpretadas por VetSmart AI.";

                    return;
                }


                if (EsConsultaDeCartilla(pregunta) &&
                    !SolicitaInterpretacion(pregunta))
                {
                    string resultado =
                        await consultaService.ObtenerProximasFechasCartillaAsync(50);

                    TxtRespuestaOllama.Text = resultado;

                    TxtEstadoOllama.Text =
                        "Cartilla consultada directamente desde SQLite.";

                    return;
                }


                if (EsConsultaDeListadoMascotas(pregunta) &&
                    !SolicitaInterpretacion(pregunta))
                {
                    string resultado =
                        await consultaService.ObtenerListadoMascotasAsync(100);

                    TxtRespuestaOllama.Text = resultado;

                    TxtEstadoOllama.Text =
                        "Listado obtenido directamente de SQLite.";

                    return;
                }


                if (EsConsultaDeResumenGeneral(pregunta) &&
                    !SolicitaInterpretacion(pregunta))
                {
                    string resultado =
                        await consultaService.ObtenerResumenGeneralAsync();

                    TxtRespuestaOllama.Text = resultado;

                    TxtEstadoOllama.Text =
                        "Resumen generado con los registros reales de SQLite.";

                    return;
                }


                if (!string.IsNullOrWhiteSpace(idMascota))
                {
                    string? respuestaDirecta =
                        await ResponderDatoDirectoAsync(
                            pregunta,
                            idMascota);

                    if (respuestaDirecta != null)
                    {
                        TxtRespuestaOllama.Text = respuestaDirecta;

                        TxtEstadoOllama.Text =
                            "Dato consultado directamente en SQLite " +
                            "(respuesta inmediata, sin usar el modelo).";

                        return;
                    }

                    string contextoVeterinario =
                        await App.Database.ObtenerContextoVeterinarioAsync(
                            idMascota,
                            pregunta);

                    string respuesta =
                        await ollamaService.GenerarRespuestaAsync(
                            contextoVeterinario,
                            pregunta,
                            MostrarRespuestaParcial);

                    TxtRespuestaOllama.Text = respuesta;

                    string nombre =
                        mascotaSeleccionada?.Nombre ??
                        mascotaMencionada?.Nombre ??
                        idMascota;

                    TxtEstadoOllama.Text =
                        "Respuesta basada en los registros disponibles de " +
                        nombre + " recuperados desde SQLite.";

                    return;
                }


                if (consultaGeneral &&
                    PareceConsultaDeDatos(pregunta))
                {
                    StringBuilder contextoGeneral = new();

                    contextoGeneral.AppendLine(
                        "INFORMACIÓN GENERAL DE LA VETERINARIA.");

                    contextoGeneral.AppendLine(
                        "Los siguientes datos fueron recuperados directamente de SQLite.");

                    contextoGeneral.AppendLine();

                    if (EsConsultaDeResumenGeneral(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerResumenGeneralAsync());
                    }

                    if (EsConsultaDeCitasRegistradas(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerProximasCitasAsync(30));
                    }

                    if (EsConsultaDeCartilla(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerProximasFechasCartillaAsync(50));
                    }

                    if (EsConsultaDeListadoMascotas(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerListadoMascotasAsync(100));
                    }

                    if (!EsConsultaDeCitasRegistradas(pregunta) &&
                        !EsConsultaDeCartilla(pregunta) &&
                        !EsConsultaDeListadoMascotas(pregunta) &&
                        !EsConsultaDeResumenGeneral(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerResumenGeneralAsync());

                        contextoGeneral.AppendLine();

                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerListadoMascotasAsync(100));
                    }

                    contextoGeneral.AppendLine();

                    contextoGeneral.AppendLine(
                        "No inventes datos ni afirmes que se consultaron registros que no aparecen en esta información.");

                    string respuesta =
                        await ollamaService.GenerarRespuestaAsync(
                            contextoGeneral.ToString(),
                            pregunta,
                            MostrarRespuestaParcial);

                    TxtRespuestaOllama.Text = respuesta;

                    TxtEstadoOllama.Text =
                        "Respuesta basada en información general de SQLite.";

                    return;
                }


                string contextoOrientacion =
                    "No hay una mascota seleccionada ni se identificó " +
                    "una mascota específica en la pregunta. " +
                    "Responde como orientación veterinaria general. " +
                    "No afirmes que consultaste expedientes, citas, " +
                    "vacunas o historiales de una mascota. " +
                    "No inventes registros de la veterinaria.";

                string respuestaGeneral =
                    await ollamaService.GenerarRespuestaAsync(
                        contextoOrientacion,
                        pregunta,
                            MostrarRespuestaParcial);

                TxtRespuestaOllama.Text = respuestaGeneral;

                TxtEstadoOllama.Text =
                    "Orientación veterinaria general generada por el modelo local.";
            }
            catch (Exception ex)
            {
                TxtRespuestaOllama.Text =
                    "No fue posible completar la consulta.";

                TxtEstadoOllama.Text =
                    "Error al consultar VetSmart AI: " + ex.Message +
                    "\n\nComprueba que SQLite esté inicializado y que Ollama esté abierto.";
            }
            finally
            {
                consultandoOllama = false;
                BtnConsultarOllama.IsEnabled = true;
            }
        }


        private async void BtnGuardarObservacion_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (guardandoObservacion)
                return;

            if (mascotaSeleccionada == null)
            {
                TxtMensajeGuardar.Text =
                    "Primero selecciona una mascota.";

                return;
            }

            string observacion =
                NormalizarEspacios(TxtObservacion.Text);

            string diagnostico =
                NormalizarEspacios(TxtDiagnostico.Text);

            if (string.IsNullOrWhiteSpace(observacion))
            {
                TxtMensajeGuardar.Text =
                    "Escribe una observación clínica antes de guardar.";

                TxtObservacion.Focus(
                    Microsoft.UI.Xaml.FocusState.Programmatic);

                return;
            }

            if (observacion.Length > 4000)
            {
                TxtMensajeGuardar.Text =
                    "La observación no puede superar los 4000 caracteres.";

                return;
            }

            if (diagnostico.Length > 200)
            {
                TxtMensajeGuardar.Text =
                    "El diagnóstico no puede superar los 200 caracteres.";

                return;
            }

            guardandoObservacion = true;

            BtnGuardarObservacion.IsEnabled = false;

            string idMascotaGuardada = mascotaSeleccionada.Id;
            string nombreMascotaGuardada = mascotaSeleccionada.Nombre;
            string especieMascotaGuardada = mascotaSeleccionada.Especie;

            try
            {
                if (App.Database == null)
                {
                    throw new InvalidOperationException(
                        "La base de datos no está inicializada.");
                }

                await App.Database.GuardarObservacionClinicaAsync(
                    idMascotaGuardada,
                    nombreMascotaGuardada,
                    especieMascotaGuardada,
                    observacion,
                    diagnostico);

                TxtMensajeGuardar.Text =
                    "Observación guardada correctamente en SQLite.";

                TxtObservacion.Text = "";
                TxtDiagnostico.Text = "";

                await MostrarHistorialAsync();
            }
            catch (Exception ex)
            {
                TxtMensajeGuardar.Text =
                    "No se pudo guardar la observación: " +
                    ex.Message;
            }
            finally
            {
                guardandoObservacion = false;
                BtnGuardarObservacion.IsEnabled = true;
            }
        }


        private void MostrarMensajeHistorial(string mensaje)
        {
            PanelHistorial.Children.Clear();
            TxtHistorial.Text = mensaje;
        }

        private Border CrearTarjetaObservacion(ObservacionClinicaDb registro)
        {
            StackPanel texto = new() { Spacing = 3 };

            texto.Children.Add(CrearTexto(
                $"Fecha: {registro.Fecha:dd/MM/yyyy HH:mm}",
                true));

            texto.Children.Add(CrearTexto(
                $"Observación: {registro.Observacion}"));

            if (!string.IsNullOrWhiteSpace(registro.Diagnostico) &&
                !registro.Diagnostico.Equals(
                    "No registrado",
                    StringComparison.OrdinalIgnoreCase))
            {
                texto.Children.Add(CrearTexto(
                    $"Diagnóstico registrado: {registro.Diagnostico}"));
            }

            Button eliminar = new()
            {
                Content = "Eliminar",
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(14, 6, 14, 6),
                Tag = registro,

                Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 253, 232, 232)),

                Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 176, 32, 32)),

                BorderBrush = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 240, 190, 190))
            };

            eliminar.Click += BtnEliminarObservacion_Click;

            Grid fila = new()
            {
                ColumnSpacing = 12
            };

            fila.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            fila.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            Grid.SetColumn(texto, 0);
            Grid.SetColumn(eliminar, 1);

            fila.Children.Add(texto);
            fila.Children.Add(eliminar);

            return new Border
            {
                Child = fila,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),

                Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 255, 248, 239)),

                BorderBrush = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 246, 230, 207))
            };
        }

        private async void BtnEliminarObservacion_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button boton ||
                boton.Tag is not ObservacionClinicaDb registro ||
                mascotaSeleccionada == null ||
                XamlRoot == null)
            {
                return;
            }

            ContentDialog dialogo = new()
            {
                Title = "Eliminar observación",

                Content =
                    "¿Seguro que quieres eliminar esta observación?\n\n" +
                    $"Fecha: {registro.Fecha:dd/MM/yyyy HH:mm}\n" +
                    $"Observación: {registro.Observacion}\n\n" +
                    "Esta acción no se puede deshacer.",

                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
                return;

            boton.IsEnabled = false;

            try
            {
                if (App.Database == null)
                {
                    throw new InvalidOperationException(
                        "La base de datos no está inicializada.");
                }

                await App.Database.EliminarObservacionClinicaAsync(
                    registro.Id,
                    mascotaSeleccionada.Id);

                await MostrarHistorialAsync();
            }
            catch (Exception ex)
            {
                boton.IsEnabled = true;

                TxtHistorial.Text =
                    "No se pudo eliminar la observación: " + ex.Message;
            }
        }

        private async Task MostrarHistorialAsync()
        {
            if (mascotaSeleccionada == null)
                return;

            string idMascota = mascotaSeleccionada.Id;

            try
            {
                if (App.Database == null)
                {
                    throw new InvalidOperationException(
                        "La base de datos no está inicializada.");
                }

                var registros =
                    await App.Database.ObtenerObservacionesClinicasAsync(
                        idMascota);


                if (mascotaSeleccionada == null ||
                    !mascotaSeleccionada.Id.Equals(
                        idMascota,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (registros.Count == 0)
                {
                    MostrarMensajeHistorial(
                        "Esta mascota todavía no tiene observaciones clínicas registradas.");

                    return;
                }

                PanelHistorial.Children.Clear();

                TxtHistorial.Text =
                    $"Observaciones registradas: {registros.Count}";

                foreach (var r in registros)
                {
                    PanelHistorial.Children.Add(
                        CrearTarjetaObservacion(r));
                }
            }
            catch (Exception ex)
            {
                MostrarMensajeHistorial(
                    "Error al consultar historial en SQLite: " +
                    ex.Message);
            }
        }


        private static string NormalizarTexto(string texto)
        {
            string normalizado = texto
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            StringBuilder resultado = new();

            foreach (char c in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    resultado.Append(c);
                }
            }

            return resultado.ToString()
                .Normalize(NormalizationForm.FormC);
        }


        private static InterpretacionClinica Interpretar(
            string comentario)
        {
            InterpretacionClinica interpretacion = new();

            string texto = NormalizarTexto(comentario);

            string[] frases = Regex.Split(
                texto,
                @"[.,;:!?]|\bpero\b|\bsin embargo\b");

            foreach (string fraseOriginal in frases)
            {
                string frase = fraseOriginal.Trim();

                if (string.IsNullOrWhiteSpace(frase))
                    continue;

                var menciones =
                    new List<(int Inicio, int Fin, string Sintoma)>();

                foreach (var patron in Patrones)
                {
                    foreach (Match match in Regex.Matches(
                        frase,
                        patron.Value))
                    {
                        menciones.Add((
                            match.Index,
                            match.Index + match.Length,
                            patron.Key));
                    }
                }

                var ordenadas = menciones
                    .OrderBy(m => m.Inicio)
                    .ThenByDescending(m => m.Fin - m.Inicio)
                    .ToList();

                var filtradas =
                    new List<(int Inicio, int Fin, string Sintoma)>();

                foreach (var m in ordenadas)
                {
                    if (!filtradas.Any(f =>
                        m.Inicio < f.Fin &&
                        m.Fin > f.Inicio))
                    {
                        filtradas.Add(m);
                    }
                }

                bool negacionConNi =
                    Regex.IsMatch(
                        frase,
                        @"\b(no presenta|no tiene|niega|sin|ausencia de)\b") &&
                    Regex.IsMatch(frase, @"\bni\b");

                bool incertidumbreCompartida = Regex.IsMatch(
                    frase,
                    @"\b(sospecha|posible|posiblemente|podria|probable|aparentemente)\b");

                foreach (var mencion in filtradas)
                {
                    string antes =
                        frase.Substring(0, mencion.Inicio);

                    string[] palabras = antes.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries);

                    string contexto = string.Join(
                        " ",
                        palabras.TakeLast(5));

                    bool negativo = Regex.IsMatch(
                        contexto,
                        @"\b(no presenta|no tiene|niega|sin|ausencia de|no hay|no se observa|no se observan)\b");

                    negativo = negativo ||
                        Regex.IsMatch(contexto, @"\bno\s*$");

                    if (negacionConNi)
                        negativo = true;

                    bool incierto = Regex.IsMatch(
                        contexto,
                        @"\b(sospecha|posible|posiblemente|podria|probable|aparentemente)\b");

                    if (incertidumbreCompartida && !negativo)
                        incierto = true;

                    if (negativo)
                    {
                        interpretacion.Ausentes.Add(
                            mencion.Sintoma);
                    }
                    else if (incierto)
                    {
                        interpretacion.Inciertos.Add(
                            mencion.Sintoma);
                    }
                    else
                    {
                        interpretacion.Presentes.Add(
                            mencion.Sintoma);
                    }
                }
            }

            var contradicciones = interpretacion.Presentes
                .Intersect(interpretacion.Ausentes)
                .ToList();

            foreach (string sintoma in contradicciones)
            {
                interpretacion.Presentes.Remove(sintoma);
                interpretacion.Ausentes.Remove(sintoma);
                interpretacion.Inciertos.Add(sintoma);
            }

            return interpretacion;
        }


        private static CasoRelacionado CompararCaso(
            string consulta,
            RegistroConocimiento registro)
        {
            var actual = Interpretar(consulta);
            var anterior = Interpretar(registro.Observacion);

            bool contradiccion =
                actual.Presentes.Overlaps(anterior.Ausentes) ||
                actual.Ausentes.Overlaps(anterior.Presentes);

            if (contradiccion)
            {
                return new CasoRelacionado
                {
                    Registro = registro,
                    Similitud = 0
                };
            }

            var comunes = actual.Presentes
                .Intersect(anterior.Presentes)
                .OrderBy(s => s)
                .ToList();

            double similitud = 0;

            if (actual.Presentes.Count > 0)
            {
                similitud =
                    (double)comunes.Count /
                    actual.Presentes.Count;
            }

            return new CasoRelacionado
            {
                Registro = registro,
                Similitud = similitud,
                Coincidencias = comunes
            };
        }

        private static void AplicarAfinidad(
            CasoRelacionado caso,
            MascotaOpcion? actual)
        {
            if (actual == null)
                return;

            var registro = caso.Registro;

            if (!string.IsNullOrWhiteSpace(actual.Especie) &&
                NormalizarTexto(registro.Especie).Trim() ==
                NormalizarTexto(actual.Especie).Trim())
            {
                caso.MismaEspecie = true;
            }

            if (!string.IsNullOrWhiteSpace(actual.Raza) &&
                NormalizarTexto(registro.Raza) ==
                NormalizarTexto(actual.Raza))
            {
                caso.Afinidad += 0.05;
                caso.Afinidades.Add("misma raza");
            }

            if (!string.IsNullOrWhiteSpace(actual.Sexo) &&
                NormalizarTexto(registro.Sexo) ==
                NormalizarTexto(actual.Sexo))
            {
                caso.Afinidad += 0.02;
                caso.Afinidades.Add("mismo sexo");
            }

            if (LeerEdad(actual.Edad, out double edadActual, out string unidadActual) &&
                LeerEdad(registro.Edad, out double edadCaso, out string unidadCaso) &&
                unidadActual == unidadCaso &&
                Math.Abs(edadActual - edadCaso) <= 2)
            {
                caso.Afinidad += 0.03;
                caso.Afinidades.Add("edad similar");
            }
        }

        private static bool LeerEdad(
            string texto,
            out double valor,
            out string unidad)
        {
            valor = 0;
            unidad = "";

            if (string.IsNullOrWhiteSpace(texto))
                return false;

            Match m = Regex.Match(
                texto,
                @"(\d+(?:[.,]\d+)?)\s*([A-Za-zÁÉÍÓÚáéíóúñÑ]*)");

            if (!m.Success)
                return false;

            if (!double.TryParse(
                    m.Groups[1].Value.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out valor))
            {
                return false;
            }

            unidad = NormalizarTexto(m.Groups[2].Value);

            return true;
        }


        private async void BtnBuscarSimilares_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (buscandoSimilares)
                return;

            PanelCasosSimilares.Children.Clear();


            string consulta =
                NormalizarEspacios(TxtConsultaIA.Text);

            if (string.IsNullOrWhiteSpace(consulta))
            {
                TxtCasosSimilares.Text =
                    "Escribe los síntomas o el motivo de consulta.";

                TxtConsultaIA.Focus(
                    Microsoft.UI.Xaml.FocusState.Programmatic);

                return;
            }

            if (consulta.Length > 1000)
            {
                TxtCasosSimilares.Text =
                    "La consulta no puede superar los 1000 caracteres.";

                return;
            }

            var interpretacion = Interpretar(consulta);

            if (interpretacion.Presentes.Count == 0)
            {
                TxtCasosSimilares.Text =
                    "No se identificaron síntomas presentes y confirmados en la consulta.\n\n" +
                    "Si los síntomas están negados o son inciertos, VetSmart AI no los utilizará " +
                    "para recuperar casos como si fueran síntomas confirmados.\n\n" +
                    "Esto no descarta enfermedades.";

                return;
            }

            buscandoSimilares = true;

            BtnBuscarSimilares.IsEnabled = false;

            TxtCasosSimilares.Text =
                "Buscando antecedentes clínicos en SQLite...";

            try
            {
                if (App.Database == null)
                {
                    throw new InvalidOperationException(
                        "La base de datos no está inicializada.");
                }


                var antecedentes =
                    await App.Database.ObtenerTodosLosAntecedentesClinicosAsync();

                var registros = antecedentes
                    .Where(a => !string.IsNullOrWhiteSpace(a.Texto))
                    .Select(a => new RegistroConocimiento
                    {
                        Fuente = a.Fuente,
                        IdMascota = a.IdMascota,
                        NombreMascota = a.NombreMascota,
                        Especie = a.Especie,
                        Raza = a.Raza,
                        Edad = a.Edad,
                        Sexo = a.Sexo,
                        Fecha = a.Fecha,
                        Observacion = a.Texto,
                        Diagnostico = a.Diagnostico,
                        Tratamiento = a.Tratamiento,
                        Alergias = a.Alergias
                    })
                    
                    .GroupBy(r => (
                        r.IdMascota.ToUpperInvariant(),
                        r.Fecha.Date,
                        NormalizarTexto(r.Observacion)))
                    .Select(g => g.First())
                    .ToList();

                var casos = registros
                    .Select(r => CompararCaso(consulta, r))
                    .Where(c =>
                        c.Similitud >= 0.40 &&
                        c.Coincidencias.Count > 0)
                    .Select(c =>
                    {
                        AplicarAfinidad(c, mascotaSeleccionada);
                        return c;
                    })
                    .OrderByDescending(c => c.MismaEspecie)
                    .ThenByDescending(c => c.Similitud + c.Afinidad)
                    .ThenByDescending(c => c.Coincidencias.Count)
                    .ThenByDescending(c => c.Registro.Fecha)
                    .Take(6)
                    .ToList();

                int mascotasRevisadas = registros
                    .Select(r => r.IdMascota.ToUpperInvariant())
                    .Distinct()
                    .Count();

                string alcance =
                    $"Se revisaron {registros.Count} registros clínicos " +
                    $"de {mascotasRevisadas} mascotas " +
                    $"(observaciones clínicas e historial médico).";

                if (casos.Count == 0)
                {
                    TxtCasosSimilares.Text =
                        "No se encontraron antecedentes compatibles con los síntomas " +
                        "identificados por las reglas actuales.\n\n" +
                        alcance + "\n\n" +
                        "Esto no descarta enfermedades ni sustituye una valoración veterinaria.";

                    return;
                }

                TxtCasosSimilares.Text =
                    $"Se encontraron {casos.Count} antecedentes relacionados. " +
                    alcance;

                int numero = 1;

                foreach (var caso in casos)
                {
                    CrearTarjetaClinica(caso, numero);
                    numero++;
                }
            }
            catch (Exception ex)
            {
                TxtCasosSimilares.Text =
                    "Error al buscar antecedentes en SQLite: " +
                    ex.Message;
            }
            finally
            {
                buscandoSimilares = false;
                BtnBuscarSimilares.IsEnabled = true;
            }
        }


        private void CrearTarjetaClinica(
            CasoRelacionado caso,
            int numero)
        {
            var registro = caso.Registro;

            bool mismaMascota =
                mascotaSeleccionada != null &&
                registro.IdMascota.Equals(
                    mascotaSeleccionada.Id,
                    StringComparison.OrdinalIgnoreCase);

            Border tarjeta = new Border
            {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 5, 0, 5),

                Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        255, 255, 248, 239)),

                BorderBrush = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        255, 246, 230, 207))
            };

            StackPanel contenido = new StackPanel
            {
                Spacing = 9
            };

            TextBlock titulo = new TextBlock
            {
                Text = $"CASO RELACIONADO #{numero}",
                FontSize = 16,

                FontWeight =
                    Microsoft.UI.Text.FontWeights.Bold,

                Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        255, 224, 113, 15))
            };

            contenido.Children.Add(titulo);

            bool otraEspecie =
                mascotaSeleccionada != null &&
                !string.IsNullOrWhiteSpace(registro.Especie) &&
                !caso.MismaEspecie;

            TextBlock tipo = new TextBlock
            {
                Text = mismaMascota
                    ? "ANTECEDENTE DE ESTA MASCOTA"
                    : otraEspecie
                        ? "CASO DE OTRA MASCOTA (OTRA ESPECIE)"
                        : "CASO DE OTRA MASCOTA",

                FontSize = 12,

                FontWeight =
                    Microsoft.UI.Text.FontWeights.SemiBold,

                Foreground = new SolidColorBrush(
                    mismaMascota
                        ? Windows.UI.Color.FromArgb(
                            255, 242, 140, 40)
                        : Windows.UI.Color.FromArgb(
                            255, 122, 112, 102))
            };

            contenido.Children.Add(tipo);

            contenido.Children.Add(CrearTexto(
                $"Mascota: {registro.NombreMascota} " +
                $"({registro.IdMascota})",
                true));

            string perfil = string.Join(
                " · ",
                new[]
                {
                    registro.Especie,
                    registro.Raza,
                    registro.Edad,
                    registro.Sexo
                }.Where(t => !string.IsNullOrWhiteSpace(t)));

            if (perfil.Length > 0)
                contenido.Children.Add(CrearTexto(perfil));

            string formatoFecha =
                registro.Fecha.TimeOfDay == TimeSpan.Zero
                    ? "dd/MM/yyyy"
                    : "dd/MM/yyyy HH:mm";

            contenido.Children.Add(CrearTexto(
                $"Fecha: {registro.Fecha.ToString(formatoFecha)}"));

            if (!string.IsNullOrWhiteSpace(registro.Fuente))
                contenido.Children.Add(CrearTexto(
                    $"Fuente: {registro.Fuente}"));

            contenido.Children.Add(new Border
            {
                Height = 1,

                Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        255, 240, 226, 206)),

                Margin = new Thickness(0, 5, 0, 5)
            });

            contenido.Children.Add(CrearTexto(
                registro.Fuente == "Historial médico"
                    ? "MOTIVO, SÍNTOMAS Y OBSERVACIONES"
                    : "OBSERVACIÓN VETERINARIA",
                true));

            contenido.Children.Add(CrearTexto(
                registro.Observacion));

            contenido.Children.Add(CrearTexto(
                "DIAGNÓSTICO DOCUMENTADO",
                true));

            contenido.Children.Add(CrearTexto(
                string.IsNullOrWhiteSpace(registro.Diagnostico)
                    ? "No registrado"
                    : registro.Diagnostico));

            if (!string.IsNullOrWhiteSpace(registro.Tratamiento))
            {
                contenido.Children.Add(CrearTexto(
                    "TRATAMIENTO REGISTRADO",
                    true));

                contenido.Children.Add(CrearTexto(
                    registro.Tratamiento));
            }

            if (!string.IsNullOrWhiteSpace(registro.Alergias))
            {
                contenido.Children.Add(CrearTexto(
                    $"Alergias registradas: {registro.Alergias}"));
            }

            contenido.Children.Add(CrearTexto(
                $"Coincidencia de síntomas identificados: " +
                $"{caso.Similitud:P0}",
                true));

            string terminos = string.Join(
                ", ",
                caso.Coincidencias.Select(t =>
                    t.Replace("_", " ")));

            contenido.Children.Add(CrearTexto(
                $"Síntomas relacionados: {terminos}"));

            if (caso.Afinidades.Count > 0)
            {
                contenido.Children.Add(CrearTexto(
                    "Parecido con esta mascota: " +
                    string.Join(", ", caso.Afinidades)));
            }

            contenido.Children.Add(CrearTexto(
                "Resultado orientativo basado en reglas de comparación de texto."));

            tarjeta.Child = contenido;

            PanelCasosSimilares.Children.Add(tarjeta);
        }



        private static TextBlock CrearTexto(
            string contenido,
            bool negritas = false)
        {
            TextBlock texto = new TextBlock
            {
                Text = contenido,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,

                Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        255, 43, 33, 24))
            };

            if (negritas)
            {
                texto.FontWeight =
                    Microsoft.UI.Text.FontWeights.SemiBold;
            }

            return texto;
        }
    }
}