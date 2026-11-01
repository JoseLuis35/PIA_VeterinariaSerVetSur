using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        }

        private readonly List<MascotaOpcion> mascotas = new();
        private readonly OllamaService ollamaService = new();
        private MascotaOpcion? mascotaSeleccionada;

        // Nombres de las imágenes de Assets que no se pudieron cargar
        private readonly HashSet<string> imagenesFallidas = new();

        private string RutaMascotas => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "mascotas.txt");

        private string RutaConocimiento => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "vetsmart_conocimiento.json");

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
            CargarMascotas();
        }

        // =========================================================
        // AVISO CUANDO UNA IMAGEN DE ASSETS NO SE PUEDE CARGAR
        // =========================================================

        // Para controles Image (usa la propiedad Tag con el nombre del archivo)
        private void Imagen_ImageFailed(
            object sender, ExceptionRoutedEventArgs e)
        {
            string nombre =
                (sender as FrameworkElement)?.Tag as string ?? "imagen";

            RegistrarImagenFallida(nombre);
        }

        // Para la imagen de fondo del encabezado (ImageBrush)
        private void FondoEncabezado_ImageFailed(
            object sender, ExceptionRoutedEventArgs e)
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

        private void CargarMascotas()
        {
            string idAnterior = mascotaSeleccionada?.Id ?? "";

            mascotaSeleccionada = null;
            mascotas.Clear();

            CmbMascotas.ItemsSource = null;
            PanelCasosSimilares.Children.Clear();

            TxtDatosMascota.Text =
                "Selecciona una mascota para consultar sus datos.";

            TxtHistorial.Text =
                "Selecciona una mascota para consultar su historial.";

            TxtCasosSimilares.Text =
                "Aquí aparecerán los antecedentes relacionados.";

            try
            {
                if (!File.Exists(RutaMascotas))
                {
                    TxtMensajeMascotas.Text =
                        "No se encontró mascotas.txt. " +
                        "Registra una mascota primero.";
                    return;
                }

                foreach (string linea in File.ReadLines(RutaMascotas))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                        continue;

                    string[] datos = linea.Split(',');

                    if (datos.Length < 8 ||
                        string.IsNullOrWhiteSpace(datos[0]) ||
                        string.IsNullOrWhiteSpace(datos[1]))
                        continue;

                    mascotas.Add(new MascotaOpcion
                    {
                        Id = datos[0].Trim(),
                        Nombre = datos[1].Trim(),
                        Especie = datos[2].Trim(),
                        Raza = datos[3].Trim(),
                        Edad = datos[4].Trim(),
                        Sexo = datos[5].Trim(),
                        Peso = datos[6].Trim(),
                        IdPropietario = datos[7].Trim()
                    });
                }

                var ordenadas = mascotas
                    .OrderBy(m => m.Nombre)
                    .ToList();

                CmbMascotas.ItemsSource = ordenadas;

                TxtMensajeMascotas.Text =
                    $"Mascotas disponibles: {ordenadas.Count}.";

                var anterior = ordenadas.FirstOrDefault(m =>
                    m.Id.Equals(
                        idAnterior,
                        StringComparison.OrdinalIgnoreCase));

                if (anterior != null)
                    CmbMascotas.SelectedItem = anterior;
            }
            catch (Exception ex)
            {
                TxtMensajeMascotas.Text =
                    "Error al cargar mascotas: " + ex.Message;
            }
        }

        private void BtnActualizarMascotas_Click(
            object sender, RoutedEventArgs e)
        {
            CargarMascotas();
        }

        private void CmbMascotas_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
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

                TxtHistorial.Text =
                    "Selecciona una mascota para consultar su historial.";
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

            MostrarHistorial();
        }

        private List<RegistroConocimiento> LeerConocimiento()
        {
            if (!File.Exists(RutaConocimiento))
                return new List<RegistroConocimiento>();

            string json = File.ReadAllText(RutaConocimiento);

            return JsonSerializer.Deserialize<List<RegistroConocimiento>>(
                json) ?? new List<RegistroConocimiento>();
        }

        // =========================================================
        // VETSMART AI 2.0: IDENTIFICACIÓN DE INTENCIONES
        // =========================================================

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
            string preguntaNormalizada = NormalizarPregunta(pregunta);

            // Primero se revisan los identificadores.
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

            // Después se buscan nombres completos.
            // Se ordenan por longitud para priorizar nombres más específicos.
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

        private static bool EsConsultaDeCitas(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(citas|cita|agenda|agendadas|agendados|proxima consulta|proximas consultas|proximas citas|pendientes)\b");
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
                @"\b(lista|listado|registradas|registrados|registradas en el sistema|cuantas mascotas|que mascotas|cuales mascotas|mascotas tenemos)\b");
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

            return EsConsultaDeCitas(pregunta) ||
                   EsConsultaDeCartilla(pregunta) ||
                   EsConsultaDeListadoMascotas(pregunta) ||
                   EsConsultaDeResumenGeneral(pregunta) ||
                   Regex.IsMatch(
                       texto,
                       @"\b(historial|antecedentes|diagnostico registrado|tratamiento registrado|propietario|telefono|correo|peso registrado|datos de|informacion de|expediente)\b");
        }

        private static bool SolicitaInterpretacion(string pregunta)
        {
            string texto = NormalizarPregunta(pregunta);

            return Regex.IsMatch(
                texto,
                @"\b(por que|porque|que significa|que puede significar|explica|explicame|recomiendas|que podria tener|que enfermedad|posibles causas|como puedo ayudar|que debo hacer|interpreta|analiza)\b");
        }

        // =========================================================
        // CONSULTA INTELIGENTE A SQLITE Y OLLAMA
        // =========================================================

        private async void BtnConsultarOllama_Click(
            object sender, RoutedEventArgs e)
        {
            string pregunta = TxtPreguntaOllama.Text.Trim();

            if (string.IsNullOrWhiteSpace(pregunta))
            {
                TxtEstadoOllama.Text =
                    "Escribe una pregunta antes de consultar.";
                return;
            }

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

                // Las pantallas actuales todavía guardan en TXT.
                // Se sincronizan esos archivos antes de consultar SQLite.
                await App.Database.SincronizarDatosDesdeArchivosAsync();

                var consultaService = new ConsultaVeterinariaService(
                    App.Database);

                string preguntaNormalizada =
                    NormalizarPregunta(pregunta);

                // Si la pregunta menciona una mascota por nombre o ID,
                // se identifica sin obligar al usuario a seleccionarla.
                MascotaOpcion? mascotaMencionada =
                    BuscarMascotaEnPregunta(pregunta);

                string idMascota = mascotaSeleccionada?.Id ?? "";

                // La selección manual tiene prioridad.
                // Si no hay selección, se usa la mascota mencionada.
                if (string.IsNullOrWhiteSpace(idMascota) &&
                    mascotaMencionada != null)
                {
                    idMascota = mascotaMencionada.Id;
                }

                bool consultaGeneral =
                    string.IsNullOrWhiteSpace(idMascota);

                // -------------------------------------------------
                // 1. CONSULTAS DIRECTAS: PRÓXIMAS CITAS
                // -------------------------------------------------

                if (EsConsultaDeCitas(pregunta) &&
                    !SolicitaInterpretacion(pregunta))
                {
                    string resultado =
                        await consultaService.ObtenerProximasCitasAsync(30);

                    TxtRespuestaOllama.Text = resultado;

                    TxtEstadoOllama.Text =
                        "Consulta resuelta directamente desde SQLite. " +
                        "No fue necesario utilizar el modelo de IA.";

                    return;
                }

                // -------------------------------------------------
                // 2. CONSULTAS DIRECTAS: CARTILLA MÉDICA
                // -------------------------------------------------

                if (EsConsultaDeCartilla(pregunta) &&
                    !SolicitaInterpretacion(pregunta))
                {
                    string resultado =
                        await consultaService.ObtenerProximasFechasCartillaAsync(50);

                    TxtRespuestaOllama.Text = resultado;

                    TxtEstadoOllama.Text =
                        "Cartilla consultada directamente desde SQLite. " +
                        "Las fechas mostradas corresponden a registros guardados.";

                    return;
                }

                // -------------------------------------------------
                // 3. CONSULTAS DIRECTAS: LISTADO DE MASCOTAS
                // -------------------------------------------------

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

                // -------------------------------------------------
                // 4. CONSULTAS DIRECTAS: RESUMEN GENERAL
                // -------------------------------------------------

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

                // -------------------------------------------------
                // 5. PREGUNTAS SOBRE UNA MASCOTA ESPECÍFICA
                // -------------------------------------------------

                if (!string.IsNullOrWhiteSpace(idMascota))
                {
                    string contextoVeterinario =
                        await App.Database.ObtenerContextoVeterinarioAsync(
                            idMascota);

                    // Si la pregunta es únicamente de datos, se puede
                    // responder usando el expediente recuperado.
                    // Qwen3 se utiliza cuando se requiere interpretación.
                    if (PareceConsultaDeDatos(pregunta) &&
                        !SolicitaInterpretacion(pregunta))
                    {
                        string respuesta = await ollamaService.GenerarRespuestaAsync(
                            contextoVeterinario,
                            pregunta);

                        TxtRespuestaOllama.Text = respuesta;

                        string nombre = mascotaSeleccionada?.Nombre ??
                                        mascotaMencionada?.Nombre ??
                                        idMascota;

                        TxtEstadoOllama.Text =
                            "Consulta basada en el expediente de " +
                            nombre + " recuperado desde SQLite.";

                        return;
                    }

                    // Pregunta clínica o combinada:
                    // SQLite aporta los datos y Ollama los interpreta.
                    string respuestaIA = await ollamaService.GenerarRespuestaAsync(
                        contextoVeterinario,
                        pregunta);

                    TxtRespuestaOllama.Text = respuestaIA;

                    string nombreMascota = mascotaSeleccionada?.Nombre ??
                                           mascotaMencionada?.Nombre ??
                                           idMascota;

                    TxtEstadoOllama.Text =
                        "Respuesta generada por Qwen3 utilizando los registros " +
                        "disponibles de " + nombreMascota + " en SQLite.";

                    return;
                }

                // -------------------------------------------------
                // 6. CONSULTAS GENERALES SIN MASCOTA SELECCIONADA
                // -------------------------------------------------

                if (consultaGeneral && PareceConsultaDeDatos(pregunta))
                {
                    StringBuilder contextoGeneral = new();

                    contextoGeneral.AppendLine(
                        "INFORMACIÓN GENERAL DE LA VETERINARIA.");

                    contextoGeneral.AppendLine(
                        "Los siguientes datos fueron recuperados directamente de SQLite.");

                    contextoGeneral.AppendLine();

                    // Se agregan los datos generales necesarios según
                    // el contenido de la pregunta.
                    if (EsConsultaDeResumenGeneral(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerResumenGeneralAsync());

                        contextoGeneral.AppendLine();
                    }

                    if (EsConsultaDeCitas(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerProximasCitasAsync(30));

                        contextoGeneral.AppendLine();
                    }

                    if (EsConsultaDeCartilla(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerProximasFechasCartillaAsync(50));

                        contextoGeneral.AppendLine();
                    }

                    if (EsConsultaDeListadoMascotas(pregunta))
                    {
                        contextoGeneral.AppendLine(
                            await consultaService.ObtenerListadoMascotasAsync(100));

                        contextoGeneral.AppendLine();
                    }

                    // Si la pregunta general solicita varios tipos de datos,
                    // se agregan también los listados que correspondan.
                    if (!EsConsultaDeCitas(pregunta) &&
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

                    if (!SolicitaInterpretacion(pregunta))
                    {
                        string respuesta = await ollamaService.GenerarRespuestaAsync(
                            contextoGeneral.ToString(),
                            pregunta);

                        TxtRespuestaOllama.Text = respuesta;

                        TxtEstadoOllama.Text =
                            "Respuesta basada en información general de SQLite.";

                        return;
                    }

                    // Si además de consultar datos solicita explicación,
                    // se envía el contexto a Qwen3.
                    string respuestaMixta =
                        await ollamaService.GenerarRespuestaAsync(
                            contextoGeneral.ToString(),
                            pregunta);

                    TxtRespuestaOllama.Text = respuestaMixta;

                    TxtEstadoOllama.Text =
                        "Respuesta generada por Qwen3 con información general " +
                        "recuperada de SQLite.";

                    return;
                }

                // -------------------------------------------------
                // 7. PREGUNTAS GENERALES DE ORIENTACIÓN VETERINARIA
                // -------------------------------------------------

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
                        pregunta);

                TxtRespuestaOllama.Text = respuestaGeneral;

                TxtEstadoOllama.Text =
                    "Orientación veterinaria general generada por el modelo local qwen3:4b.";
            }
            catch (Exception ex)
            {
                TxtRespuestaOllama.Text =
                    "No fue posible completar la consulta.";

                TxtEstadoOllama.Text =
                    "Error al consultar VetSmart AI: " + ex.Message +
                    "\n\nComprueba que SQLite esté inicializado y que Ollama " +
                    "esté abierto con el modelo qwen3:4b.";
            }
            finally
            {
                BtnConsultarOllama.IsEnabled = true;
            }
        }

        private void BtnGuardarObservacion_Click(
            object sender, RoutedEventArgs e)
        {
            if (mascotaSeleccionada == null)
            {
                TxtMensajeGuardar.Text =
                    "Primero selecciona una mascota.";
                return;
            }

            string observacion = TxtObservacion.Text.Trim();
            string diagnostico = TxtDiagnostico.Text.Trim();

            if (string.IsNullOrWhiteSpace(observacion))
            {
                TxtMensajeGuardar.Text =
                    "Escribe una observación clínica antes de guardar.";
                return;
            }

            try
            {
                var registros = LeerConocimiento();

                registros.Add(new RegistroConocimiento
                {
                    IdMascota = mascotaSeleccionada.Id,
                    NombreMascota = mascotaSeleccionada.Nombre,
                    Especie = mascotaSeleccionada.Especie,
                    Fecha = DateTime.Now,
                    Observacion = observacion,
                    Diagnostico = diagnostico
                });

                string json = JsonSerializer.Serialize(
                    registros,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                string temporal = RutaConocimiento + ".tmp";

                File.WriteAllText(temporal, json);
                File.Move(temporal, RutaConocimiento, true);

                TxtMensajeGuardar.Text =
                    "Observación guardada correctamente.";

                TxtObservacion.Text = "";
                TxtDiagnostico.Text = "";

                MostrarHistorial();
            }
            catch (Exception ex)
            {
                TxtMensajeGuardar.Text =
                    "No se pudo guardar: " + ex.Message;
            }
        }

        private void MostrarHistorial()
        {
            if (mascotaSeleccionada == null)
                return;

            try
            {
                var registros = LeerConocimiento()
                    .Where(r => r.IdMascota.Equals(
                        mascotaSeleccionada.Id,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.Fecha)
                    .ToList();

                if (registros.Count == 0)
                {
                    TxtHistorial.Text =
                        "Esta mascota todavía no tiene " +
                        "observaciones registradas.";
                    return;
                }

                StringBuilder historial = new();

                historial.AppendLine(
                    $"Consultas registradas: {registros.Count}");
                historial.AppendLine();

                foreach (var r in registros)
                {
                    historial.AppendLine(
                        $"Fecha: {r.Fecha:dd/MM/yyyy HH:mm}");

                    historial.AppendLine(
                        $"Observación: {r.Observacion}");

                    if (!string.IsNullOrWhiteSpace(r.Diagnostico))
                    {
                        historial.AppendLine(
                            $"Diagnóstico registrado: {r.Diagnostico}");
                    }

                    historial.AppendLine();
                    historial.AppendLine("------------------------");
                    historial.AppendLine();
                }

                TxtHistorial.Text = historial.ToString();
            }
            catch (Exception ex)
            {
                TxtHistorial.Text =
                    "Error al consultar historial: " + ex.Message;
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

                var menciones = new List<(int Inicio, int Fin, string Sintoma)>();

                foreach (var patron in Patrones)
                {
                    foreach (Match match in Regex.Matches(
                        frase, patron.Value))
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

                var filtradas = new List<(int Inicio, int Fin, string Sintoma)>();

                foreach (var m in ordenadas)
                {
                    if (!filtradas.Any(f =>
                        m.Inicio < f.Fin &&
                        m.Fin > f.Inicio))
                    {
                        filtradas.Add(m);
                    }
                }

                bool negacionCompartida = Regex.IsMatch(
                    frase,
                    @"\b(no presenta|no tiene|niega|sin|ausencia de)\b");

                bool incertidumbreCompartida = Regex.IsMatch(
                    frase,
                    @"\b(sospecha|posible|posiblemente|podria|probable|aparentemente)\b");

                bool negacionConNi =
                    negacionCompartida &&
                    Regex.IsMatch(frase, @"\bni\b");

                foreach (var mencion in filtradas)
                {
                    string antes = frase.Substring(0, mencion.Inicio);

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
                similitud = (double)comunes.Count /
                    actual.Presentes.Count;
            }

            return new CasoRelacionado
            {
                Registro = registro,
                Similitud = similitud,
                Coincidencias = comunes
            };
        }

        private void BtnBuscarSimilares_Click(
            object sender, RoutedEventArgs e)
        {
            PanelCasosSimilares.Children.Clear();

            if (mascotaSeleccionada == null)
            {
                TxtCasosSimilares.Text =
                    "Primero selecciona una mascota.";
                return;
            }

            string consulta = TxtConsultaIA.Text.Trim();

            if (string.IsNullOrWhiteSpace(consulta))
            {
                TxtCasosSimilares.Text =
                    "Escribe los síntomas o el motivo de consulta.";
                return;
            }

            var interpretacion = Interpretar(consulta);

            if (interpretacion.Presentes.Count == 0)
            {
                TxtCasosSimilares.Text =
                    "No se identificaron síntomas presentes " +
                    "y confirmados en la consulta.\n\n" +
                    "Si los síntomas están negados o son " +
                    "inciertos, VetSmart AI no los utilizará " +
                    "para recuperar casos como si fueran " +
                    "síntomas confirmados.\n\n" +
                    "Esto no descarta enfermedades.";
                return;
            }

            try
            {
                var casos = LeerConocimiento()
                    .Where(r => r.Especie.Equals(
                        mascotaSeleccionada.Especie,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(r => CompararCaso(consulta, r))
                    .Where(c =>
                        c.Similitud >= 0.40 &&
                        c.Coincidencias.Count > 0)
                    .OrderByDescending(c => c.Similitud)
                    .ThenByDescending(c => c.Coincidencias.Count)
                    .ThenByDescending(c => c.Registro.Fecha)
                    .Take(5)
                    .ToList();

                if (casos.Count == 0)
                {
                    TxtCasosSimilares.Text =
                        "No se encontraron antecedentes " +
                        "compatibles con los síntomas " +
                        "identificados por las reglas actuales.\n\n" +
                        "Esto no descarta enfermedades " +
                        "ni sustituye una valoración veterinaria.";
                    return;
                }

                TxtCasosSimilares.Text =
                    $"Se encontraron {casos.Count} " +
                    "antecedentes relacionados.";

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
                    "Error al buscar antecedentes: " +
                    ex.Message;
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

            // Tarjeta en tonos durazno, igual que el resto de la app
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

            TextBlock tipo = new TextBlock
            {
                Text = mismaMascota
                    ? "ANTECEDENTE DE ESTA MASCOTA"
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
                $"({registro.IdMascota})", true));

            contenido.Children.Add(CrearTexto(
                $"Fecha: {registro.Fecha:dd/MM/yyyy HH:mm}"));

            contenido.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        255, 240, 226, 206)),
                Margin = new Thickness(0, 5, 0, 5)
            });

            contenido.Children.Add(CrearTexto(
                "OBSERVACIÓN VETERINARIA", true));

            contenido.Children.Add(CrearTexto(
                registro.Observacion));

            contenido.Children.Add(CrearTexto(
                "DIAGNÓSTICO DOCUMENTADO", true));

            contenido.Children.Add(CrearTexto(
                string.IsNullOrWhiteSpace(registro.Diagnostico)
                    ? "No registrado"
                    : registro.Diagnostico));

            contenido.Children.Add(CrearTexto(
                $"Coincidencia de síntomas identificados: " +
                $"{caso.Similitud:P0}", true));

            string terminos = string.Join(
                ", ",
                caso.Coincidencias.Select(t =>
                    t.Replace("_", " ")));

            contenido.Children.Add(CrearTexto(
                $"Síntomas relacionados: {terminos}"));

            contenido.Children.Add(CrearTexto(
                "Resultado orientativo basado en reglas " +
                "de comparación de texto."));

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