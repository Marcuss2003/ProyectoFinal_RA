using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class SafeScanAnalyzer : MonoBehaviour
{
    [System.Serializable]
    public class ProductoRegistrado
    {
        public string nombreUsuario;
        public string marcaUsuario;
        public string ingredienteDetectado;
        public string porcion;
        public string servicios;
        public string fechaRegistro;
        public string statusSeguridad; // Guarda el texto con formato de color para el historial
    }

    [System.Serializable]
    public class ListaHistorialWrapper
    {
        public List<ProductoRegistrado> listaProductos = new List<ProductoRegistrado>();
    }

    private enum TipoSuplemento { Creatina, Proteina, PreWorkoutSeguro, PreWorkoutPeligroso, Omega3, General }

    [Header("Configuración de la API")]
    [SerializeField] private string apiKey = "";
    private string urlVision = "https://vision.googleapis.com/v1/images:annotate?key=";

    [Header("Referencias Únicas del Panel de RA (Mantenlo afuera en la Raíz)")]
    [SerializeField] private GameObject canvasHologramMaestro; // 🌟 Arrastra aquí tu Canvas_HologramasAR único de la escena
    [SerializeField] private GameObject panelHologram;
    [SerializeField] private TextMeshProUGUI txtTitulo;
    [SerializeField] private TextMeshProUGUI txtBeneficios;
    [SerializeField] private TextMeshProUGUI txtDosis;
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private TextMeshProUGUI txtPorcion;
    [SerializeField] private TextMeshProUGUI txtServicios;
    [SerializeField] private TextMeshProUGUI txtIngredientes;

    [Header("Referencias del Panel 2 (Información Adicional)")]
    [SerializeField] private GameObject panel2Info;
    [SerializeField] private TextMeshProUGUI txtInfoAdicional;

    [Header("Sistema de Iconos Dinámicos (0=Ok, 1=Advertencia, 2=Tache)")]
    [SerializeField] private Image imgStatusIcon;
    [SerializeField] private Sprite[] iconosSeguridad;

    [Header("Referencias del Sistema de Historial (UI de Pantalla)")]
    [SerializeField] private TMP_InputField inputNombre;
    [SerializeField] private TMP_InputField inputMarca;
    [SerializeField] private GameObject panelMenuInicial;

    [Header("Componentes Visuales del Historial")]
    [SerializeField] private Transform contenedorHistorial;
    [SerializeField] private GameObject prefabFilaHistorial;

    [Header("Efectos Visuales de Escaneo")]
    [SerializeField] private GameObject panelCargaEscaneo;
    [SerializeField] private SafeScanUIManager uiManagerLoader;

    [Header("Paneles del Tutorial Contextual")]
    [SerializeField] private GameObject tutorialPaso1;
    [SerializeField] private GameObject tutorialPaso2;
    [SerializeField] private GameObject tutorialPaso3;

    [Header("Optimización de Captura móvil")]
    [SerializeField] private GameObject canvasInterfazPrincipal;

    private bool modoRegistroActivo = false;
    private string nombreTemporal = "";
    private string marcaTemporal = "";
    private ListaHistorialWrapper historialLocal = new ListaHistorialWrapper();
    private bool esPrimerIngreso = true;

    private string statusTemporalColor = "<color=#00F3FF>[REGISTRADO]</color>";

    private void Start()
    {
        PlayerPrefs.DeleteKey("SafeScanTutorialContextual");

        // Aseguramos que el panel maestro inicie oculto y desvinculado en la raíz de la jerarquía
        if (canvasHologramMaestro != null)
        {
            canvasHologramMaestro.transform.SetParent(null);
            canvasHologramMaestro.SetActive(false);
        }
        
        if (panelHologram != null) panelHologram.SetActive(false);
        if (panel2Info != null) panel2Info.SetActive(false);
        
        if (tutorialPaso1 != null) tutorialPaso1.SetActive(false);
        if (tutorialPaso2 != null) tutorialPaso2.SetActive(false);
        if (tutorialPaso3 != null) tutorialPaso3.SetActive(false);
        esPrimerIngreso = !PlayerPrefs.HasKey("SafeScanTutorialContextual");
        
        StartCoroutine(PantallaDeCargaInicial());
        CargarHistorialDelDispositivo();
    }

    private IEnumerator PantallaDeCargaInicial()
    {
        if (panelMenuInicial != null) panelMenuInicial.SetActive(false);
        if (panelCargaEscaneo != null) panelCargaEscaneo.SetActive(true);
        if (uiManagerLoader != null) uiManagerLoader.ActualizarProgreso(0f);

        yield return new WaitForSeconds(0.8f);

        float progresoBienvenida = 0f;
        while (progresoBienvenida < 1f)
        {
            progresoBienvenida += Time.deltaTime * 0.45f;
            if (uiManagerLoader != null) uiManagerLoader.ActualizarProgreso(progresoBienvenida);
            yield return null;
        }

        if (uiManagerLoader != null) uiManagerLoader.ActualizarProgreso(1f);
        yield return new WaitForSeconds(0.7f);

        if (panelCargaEscaneo != null) panelCargaEscaneo.SetActive(false);
        if (panelMenuInicial != null) panelMenuInicial.SetActive(true);

        esPrimerIngreso = !PlayerPrefs.HasKey("SafeScanTutorialContextual");
        if (esPrimerIngreso && tutorialPaso1 != null)
        {
            tutorialPaso1.transform.SetAsLastSibling();
            tutorialPaso1.SetActive(true);
        }
    }

    #region Interfaz del Historial Dinámico Visual

    private void ActualizarListaVisualHistorial()
    {
        if (contenedorHistorial == null || prefabFilaHistorial == null) return;
        foreach (Transform hijo in contenedorHistorial)
        {
            Destroy(hijo.gameObject);
        }

        for (int i = historialLocal.listaProductos.Count - 1; i >= 0; i--)
        {
            ProductoRegistrado prod = historialLocal.listaProductos[i];
            GameObject nuevaFila = Instantiate(prefabFilaHistorial, contenedorHistorial);
            TextMeshProUGUI textoFila = nuevaFila.GetComponent<TextMeshProUGUI>();

            if (textoFila != null)
            {
                string statusTag = string.IsNullOrEmpty(prod.statusSeguridad) ?
                    "<color=#00F3FF>[REGISTRADO]</color>" : prod.statusSeguridad;

                textoFila.text = $"{statusTag} <b>{prod.nombreUsuario.ToUpper()}</b> ({prod.marcaUsuario.ToUpper()})\n" +
                                 $"<size=85%><color=#00F3FF>{prod.ingredienteDetectado}</color> | {prod.porcion} | {prod.servicios}\n" +
                                 $"<color=#888888>Escaneado el: {prod.fechaRegistro}</color></size>\n" +
                                 $"------------------------------------------------------------";
            }
        }
    }

    public void BorrarHistorialCompleto()
    {
        PlayerPrefs.DeleteKey("HistorialSafeScan");
        historialLocal.listaProductos.Clear();
        ActualizarListaVisualHistorial();
    }

    #endregion

    #region Control de Flujo de Paneles

    public void CambiarDePanel()
    {
        if (panelHologram.activeSelf || panel2Info.activeSelf)
        {
            bool nutricionalActivo = panelHologram.activeSelf;
            panelHologram.SetActive(!nutricionalActivo);
            panel2Info.SetActive(nutricionalActivo);
        }
    }

    public void RegresarAlMenuInicial()
    {
        if (canvasHologramMaestro != null)
        {
            canvasHologramMaestro.transform.SetParent(null); // Desvinculamos el Canvas del ImageTarget activo
            canvasHologramMaestro.SetActive(false);
        }
        if (panelHologram != null) panelHologram.SetActive(false);
        if (panel2Info != null) panel2Info.SetActive(false);
        if (panelMenuInicial != null) panelMenuInicial.SetActive(true);

        CargarHistorialDelDispositivo();
    }

    #endregion

    #region Disparadores Públicos

    public void DispararCapturaDeEtiqueta()
    {
        if (tutorialPaso2 != null) tutorialPaso2.SetActive(false);
        StartCoroutine(CapturarPantallaYProcesar());
    }

    public void SeleccionarConsultaRapida()
    {
        modoRegistroActivo = false;
        nombreTemporal = "Consulta Rápida";
        marcaTemporal = "";
        if (panelMenuInicial != null) panelMenuInicial.SetActive(false);
        if (tutorialPaso1 != null) tutorialPaso1.SetActive(false);
        if (esPrimerIngreso && tutorialPaso2 != null)
        {
            tutorialPaso2.transform.SetAsLastSibling();
            tutorialPaso2.SetActive(true);
        }
    }

    public void SeleccionarRegistrarProducto()
    {
        if (string.IsNullOrEmpty(inputNombre.text) || string.IsNullOrEmpty(inputMarca.text))
        {
            return;
        }

        modoRegistroActivo = true;
        nombreTemporal = inputNombre.text;
        marcaTemporal = inputMarca.text;
        if (panelMenuInicial != null) panelMenuInicial.SetActive(false);
        if (tutorialPaso1 != null) tutorialPaso1.SetActive(false);
        if (esPrimerIngreso && tutorialPaso2 != null)
        {
            tutorialPaso2.transform.SetAsLastSibling();
            tutorialPaso2.SetActive(true);
        }
    }

    #endregion

    private IEnumerator CapturarPantallaYProcesar()
    {
        if (canvasInterfazPrincipal != null) canvasInterfazPrincipal.SetActive(false);
        if (canvasHologramMaestro != null) canvasHologramMaestro.SetActive(false);
        if (panelHologram != null) panelHologram.SetActive(false);
        if (panel2Info != null) panel2Info.SetActive(false);

        yield return new WaitForEndOfFrame();

        int targetWidth = Mathf.Min(Screen.width, 1024);
        int targetHeight = Mathf.Min(Screen.height, 1024);

        Texture2D texture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        RenderTexture rt = new RenderTexture(targetWidth, targetHeight, 24);
        RenderTexture.active = rt;
        
        Camera.main.targetTexture = rt;
        Camera.main.Render();
        
        texture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        texture.Apply();
        
        Camera.main.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        byte[] imageBytes = texture.EncodeToJPG(80);
        Destroy(texture);

        if (canvasInterfazPrincipal != null) canvasInterfazPrincipal.SetActive(true);
        if (panelCargaEscaneo != null) panelCargaEscaneo.SetActive(true);
        if (uiManagerLoader != null) uiManagerLoader.ActualizarProgreso(0f);

        string base64Image = Convert.ToBase64String(imageBytes);
        string jsonRequestBody = "{\"requests\":[{\"image\":{\"content\":\"" + base64Image + "\"},\"features\":[{\"type\":\"TEXT_DETECTION\"}]}]}";
        float progresoSimulado = 0f;
        while (progresoSimulado < 0.35f)
        {
            progresoSimulado += Time.deltaTime * 0.5f;
            if (uiManagerLoader != null) uiManagerLoader.ActualizarProgreso(progresoSimulado);
            yield return null;
        }

        using (UnityWebRequest request = new UnityWebRequest(urlVision + apiKey, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operacionAsincrona = request.SendWebRequest();
            while (!operacionAsincrona.isDone)
            {
                if (progresoSimulado < 0.90f)
                {
                    progresoSimulado += Time.deltaTime * 0.1f;
                    if (uiManagerLoader != null) uiManagerLoader.ActualizarProgreso(progresoSimulado);
                }
                yield return null;
            }

            if (uiManagerLoader != null) uiManagerLoader.ActualizarProgreso(1f);
            yield return new WaitForSeconds(0.7f);

            if (panelCargaEscaneo != null) panelCargaEscaneo.SetActive(false);

            if (request.result == UnityWebRequest.Result.Success)
            {
                ProcessDetection(request.downloadHandler.text);
            }
            else
            {
                if (panelMenuInicial != null) panelMenuInicial.SetActive(true);
            }
        }
    }

    private void ProcessDetection(string jsonResponse)
    {
        string textoLimpio = jsonResponse.ToLower();
        string[] lineas = textoLimpio.Split(new[] { "\\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        string porcionDetectada = "1 Scoop";
        string serviciosDetectados = "30 serv";
        string ingredientePrincipal = "Suplemento Analizado";

        TipoSuplemento suplementoDetectado = TipoSuplemento.General;

        foreach (string linea in lineas)
        {
            if (linea.Contains("serving size") || linea.Contains("porcion") || linea.Contains("porción") || linea.Contains("tamaño de la por")) porcionDetectada = ExtraerDatoDeLinea(linea, "size");
            if (linea.Contains("servings") || linea.Contains("porciones") || linea.Contains("container") || linea.Contains("envase")) serviciosDetectados = ExtraerDatoDeLinea(linea, "container");
            
            if (linea.Contains("eria") || linea.Contains("jarensis") || linea.Contains("yohimbe") || linea.Contains("yohimbine") || linea.Contains("rauwolfia") || linea.Contains("rauwolscine") || linea.Contains("synephrine") || linea.Contains("bitter orange"))
            {
                suplementoDetectado = TipoSuplemento.PreWorkoutPeligroso;
                break;
            }
            else if (linea.Contains("monohydrate") || linea.Contains("monohidratada") || linea.Contains("creatine") || linea.Contains("creatina") || linea.Contains("micronized"))
            {
                suplementoDetectado = TipoSuplemento.Creatina;
            }
            else if (linea.Contains("protein") || linea.Contains("proteina") || linea.Contains("proteína") || linea.Contains("whey") || linea.Contains("isolate") || linea.Contains("glutamine") || linea.Contains("leucine") || linea.Contains("suero"))
            {
                suplementoDetectado = TipoSuplemento.Proteina;
            }
            else if (linea.Contains("caffeine") || linea.Contains("cafeina") || linea.Contains("pre-workout") || linea.Contains("beta-alanine") || linea.Contains("citrulline") || linea.Contains("anhydrous"))
            {
                suplementoDetectado = TipoSuplemento.PreWorkoutSeguro;
            }
            else if (linea.Contains("omega") || linea.Contains("fish oil") || linea.Contains("epa") || linea.Contains("dha") || linea.Contains("pescado"))
            {
                suplementoDetectado = TipoSuplemento.Omega3;
            }
        }

        string marcaFiltro = marcaTemporal.ToLower();
        if (suplementoDetectado == TipoSuplemento.General)
        {
            if (marcaFiltro.Contains("peligro") || marcaFiltro.Contains("prohibido") || marcaFiltro.Contains("homicida") || marcaFiltro.Contains("bomba"))
                suplementoDetectado = TipoSuplemento.PreWorkoutPeligroso;
            else if (marcaFiltro.Contains("creatina") || marcaFiltro.Contains("creatine"))
                suplementoDetectado = TipoSuplemento.Creatina;
            else if (marcaFiltro.Contains("proteina") || marcaFiltro.Contains("protein") || marcaFiltro.Contains("whey"))
                suplementoDetectado = TipoSuplemento.Proteina;
            else if (marcaFiltro.Contains("preworkout") || marcaFiltro.Contains("pre-workout") || marcaFiltro.Contains("preentreno"))
                suplementoDetectado = TipoSuplemento.PreWorkoutSeguro;
            else if (marcaFiltro.Contains("omega") || marcaFiltro.Contains("fish oil"))
                suplementoDetectado = TipoSuplemento.Omega3;
        }

        if (porcionDetectada == "1 Scoop" || porcionDetectada.Length > 25)
        {
            if (suplementoDetectado == TipoSuplemento.PreWorkoutPeligroso) porcionDetectada = "1 Scoop (19g)";
            else if (suplementoDetectado == TipoSuplemento.Creatina) porcionDetectada = "1 Scoop (5g)";
            else if (suplementoDetectado == TipoSuplemento.Proteina) porcionDetectada = "1 Scoop (31g)";
            else if (suplementoDetectado == TipoSuplemento.PreWorkoutSeguro) porcionDetectada = "1 Scoop (10.5g)";
            else if (suplementoDetectado == TipoSuplemento.Omega3) porcionDetectada = "2 Cápsulas blandas";
        }
        if (serviciosDetectados == "30 serv" || serviciosDetectados.Length > 25)
        {
            if (suplementoDetectado == TipoSuplemento.PreWorkoutPeligroso) serviciosDetectados = "20 Servicios";
            else if (suplementoDetectado == TipoSuplemento.Proteina) serviciosDetectados = "29 Servicios aprox.";
            else if (suplementoDetectado == TipoSuplemento.Omega3) serviciosDetectados = "110 Servicios";
            else serviciosDetectados = "30 Servicios por envase";
        }

        if (textoLimpio.Length > 10 && canvasHologramMaestro != null)
        {
            // 🌟 MUDANZA AR EN CALIENTE: Mapeamos dinámicamente hacia qué ImageTarget de la mesa viajará el Canvas
            string nombreTargetDestino = "ImageTarget_Creatina";
            if (suplementoDetectado == TipoSuplemento.Proteina) nombreTargetDestino = "ImageTarget_Prote";
            else if (suplementoDetectado == TipoSuplemento.PreWorkoutSeguro) nombreTargetDestino = "ImageTarget_Prework";
            else if (suplementoDetectado == TipoSuplemento.PreWorkoutPeligroso) nombreTargetDestino = "ImageTarget_Prework2"; // 🌟 Vinculado a tu nuevo Target de Peligro
            else if (suplementoDetectado == TipoSuplemento.Omega3) nombreTargetDestino = "ImageTarget_Omega3";

            GameObject targetEncontrado = GameObject.Find(nombreTargetDestino);
            if (targetEncontrado != null)
            {
                // Teletransportamos el Canvas adentro de ese bote en este milisegundo
                canvasHologramMaestro.transform.SetParent(targetEncontrado.transform);
                
                // Forzamos el reinicio de coordenadas en World Space para que aparezca centrado arriba de su tapa
                RectTransform rect = canvasHologramMaestro.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition3D = new Vector3(0f, 0.15f, 0f); // Posición vertical sobre el envase
                    rect.localRotation = Quaternion.Euler(90f, 0f, 0f);   // Se para ortogonal como un muro frente al visor
                }
                canvasHologramMaestro.SetActive(true);
            }

            if (panelHologram != null) panelHologram.SetActive(true);
            if (panel2Info != null) panel2Info.SetActive(false);

            string nombreTitulo = (string.IsNullOrEmpty(nombreTemporal) || nombreTemporal == "Consulta Rápida") ? "SUPLEMENTO DETECTADO" : nombreTemporal.ToUpper();

            switch (suplementoDetectado)
            {
                case TipoSuplemento.PreWorkoutPeligroso:
                    statusTemporalColor = "<color=#FF4444>[RIESGO MÁXIMO]</color>";
                    ingredientePrincipal = "ESTIMULANTE PROHIBIDO / ADVERTENCIA CARDIOVASCULAR";
                    txtTitulo.text = "⚠️ EXTREME PRE-WORKOUT";
                    txtStatus.text = "<color=#FF4444>RIESGO / ADVERTENCIA MÁXIMA</color>";
                    txtDosis.text = "NO RECOMENDADO";
                    txtBeneficios.text = "• Taquicardia e hipertensión aguda.\n• Riesgo alto de crash y ansiedad.\n• Contiene sustancias prohibidas (WADA).";
                    if (iconosSeguridad.Length > 2 && iconosSeguridad[2] != null) imgStatusIcon.sprite = iconosSeguridad[2];

                    if (txtInfoAdicional != null)
                    {
                        txtInfoAdicional.text = 
                            "<color=#FF4444><b>INFORME DE TOXICIDAD Y RIESGO ALIMENTARIO</b></color>\n\n" +
                            "<b>COMPONENTES DE ALTO RIESGO DETECTADOS:</b>\n" +
                            "• <b>Eria Jarensis Extract (N-Phenethyl Dimethylamine):</b> Estimulante sintético potente prohibido por agencias deportivas. Eleva la dopamina de forma agresiva. Riesgo de adicción, vasoconstricción severa y daño neurocardiaco.\n" +
                            "• <b>Yohimbe & Rauwolscine (Alpha-Yohimbine):</b> Combinación hiper-adrenérgica. Bloquea mecanismos de frenado cardiovascular, provocando crisis de pánico, temblores e hipertensión crítica.\n" +
                            "• <b>Bitter Orange (Sinefrina) + 420mg Cafeína Pura:</b> Sinergia peligrosa que imita los efectos secundarios de la efedrina. Eleva drásticamente las probabilidades de sufrir arritmias cardíacas o infartos durante esfuerzos físicos máximos.\n\n" +
                            "<b>DICTAMEN SAFESCAN AR:</b>\n" +
                            "Este producto NO es seguro. Las dosis exceden los umbrales fisiológicos saludables y comprometen severamente el sistema nervioso central y cardiovascular.";
                    }
                    break;

                case TipoSuplemento.Creatina:
                    statusTemporalColor = "<color=#44FF44>[SEGURO]</color>";
                    ingredientePrincipal = "Creatina Monohidratada";
                    txtTitulo.text = "CREATINE MONOHYDRATE";
                    txtStatus.text = "<color=#44FF44>SEGURO / VERIFICADO</color>";
                    txtDosis.text = "5g Diarios";
                    txtBeneficios.text = "• Incrementa la fuerza muscular.\n• Acelera la recuperación celular.\n• Mejora el rendimiento ATP.";
                    if (iconosSeguridad.Length > 0 && iconosSeguridad[0] != null) imgStatusIcon.sprite = iconosSeguridad[0];

                    if (txtInfoAdicional != null)
                    {
                        txtInfoAdicional.text = 
                            "<color=#00F3FF><b>INFORMACIÓN BIOQUÍMICA DE LA CREATINA</b></color>\n\n" +
                            "<b>COMPONENTES SEGUROS Y QUÉ HACEN:</b>\n" +
                            "• <b>Creatina Monohidratada:</b> Compuesto seguro y ultra estudiado. Aumenta las reservas de fosfocreatina muscular, permitiendo regenerar el ATP de forma inmediata en contracciones anaeróbicas severas (como levantamientos pesados o trucos de calistenia).\n\n" +
                            "<b>EFECTOS SECUNDARIOS (BENIGNOS):</b>\n" +
                            "• Retención de líquidos puramente intracelular (hace que la célula muscular esté más hidratada y eficiente para sintetizar proteínas).\n\n" +
                            "<b>¿INGREDIENTES PELIGROSOS?:</b> Ninguno. No hay daño hepático, renal ni alopecia demostrada científicamente en atletas sanos.";
                    }
                    break;

                case TipoSuplemento.Proteina:
                    statusTemporalColor = "<color=#44FF44>[SEGURO]</color>";
                    ingredientePrincipal = "Proteína de Suero (Whey Protein)";
                    txtTitulo.text = "WHEY PROTEIN ISOLATE";
                    txtStatus.text = "<color=#44FF44>SEGURO / MACRONUTRIENTE</color>";
                    txtDosis.text = "1 Scoop Diario";
                    txtBeneficios.text = "• Cultiva la síntesis proteica.\n• Acelera reparación muscular.\n• Aporte práctico de aminoácidos.";
                    if (iconosSeguridad.Length > 0 && iconosSeguridad[0] != null) imgStatusIcon.sprite = iconosSeguridad[0];

                    if (txtInfoAdicional != null)
                    {
                        txtInfoAdicional.text = 
                            "<color=#00F3FF><b>ANÁLISIS DE LA ETIQUETA: WHEY PROTEIN</b></color>\n\n" +
                            "<b>COMPONENTES SEGUROS Y QUÉ HACEN:</b>\n" +
                            "• <b>Aislado y Concentrado de Suero (ON):</b> Fuente pura de macronutrientes esenciales que desencadena la vía mTOR para la hipertrofia muscular.\n" +
                            "• <b>Glutamina y BCAAs:</b> Aminoácidos clave que blindan las miofibrillas contra el catabolismo y reducen el dolor muscular post-entrenamiento.\n\n" +
                            "<b>EFECTOS SECUNDARIOS:</b>\n" +
                            "Únicamente gases o molestias digestivas leves en personas con intolerancia severa a la leche.\n\n" +
                            "<b>¿INGREDIENTES PELIGROSOS?:</b> Ninguno detectado. Cumple estrictamente con el perfil nutricional deportivo.";
                    }
                    break;

                case TipoSuplemento.PreWorkoutSeguro:
                    statusTemporalColor = "<color=#FFFF44>[PRECAUCIÓN]</color>";
                    ingredientePrincipal = "Pre-Workout (Estimulante Moderado)";
                    txtTitulo.text = "PRE-WORKOUT ENERGY";
                    txtStatus.text = "<color=#FFFF44>PRECAUCIÓN / ESTIMULANTE</color>";
                    txtDosis.text = "1 Scoop Máx.";
                    txtBeneficios.text = "• Mayor enfoque cognitivo.\n• Vasodilatación por óxido nítrico.\n• Amortigua el ácido láctico.";
                    if (iconosSeguridad.Length > 1 && iconosSeguridad[1] != null) imgStatusIcon.sprite = iconosSeguridad[1];

                    if (txtInfoAdicional != null)
                    {
                        txtInfoAdicional.text = 
                            "<color=#FFCC00><b>CIENCIA DEL PRE-ENTRENAMIENTO SEGURO</b></color>\n\n" +
                            "<b>COMPONENTES ERGOGÉNICOS SEGUROS:</b>\n" +
                            "• <b>Citrulina Malato & L-Arginina:</b> Dilatan arterias mejorando el bombeo y flujo sanguíneo hacia los músculos activos.\n" +
                            "• <b>Beta-Alanina:</b> Retrasa la fatiga neutralizando la acidez por lactato (causa comezón inofensiva o parestesia).\n" +
                            "• <b>Cafeína Anhidra & Teobromina:</b> Estimulan la energía del sistema nervioso central.\n\n" +
                            "<b>EFECTOS SECUNDARIOS:</b> Taquicardia transitoria, insomnio si se toma de noche, parestesia leve.";
                        }
                    break;

                case TipoSuplemento.Omega3:
                    statusTemporalColor = "<color=#44FF44>[SEGURO]</color>";
                    ingredientePrincipal = "Omega-3 (Ácidos Grasos Esenciales)";
                    txtTitulo.text = "OMEGA 3 FISH OIL";
                    txtStatus.text = "<color=#44FF44>ESENCIAL / CARDIOPROTECTOR</color>";
                    txtDosis.text = "2 Cápsulas";
                    txtBeneficios.text = "• Acción antiinflamatoria natural.\n• Protege sistema cardiovascular.\n• Soporte celular y cognitivo.";
                    if (iconosSeguridad.Length > 0 && iconosSeguridad[0] != null) imgStatusIcon.sprite = iconosSeguridad[0];

                    if (txtInfoAdicional != null)
                    {
                        txtInfoAdicional.text = 
                            "<color=#00F3FF><b>SOPORTE CLÍNICO: OMEGA 3 FISH OIL</b></color>\n\n" +
                            "<b>COMPONENTES SEGUROS Y QUÉ HACEN:</b>\n" +
                            "• <b>EPA (360mg por porción):</b> Disminuye los marcadores de inflamación sistémica crónica, protegiendo tendones del desgaste deportivo repetitivo.\n" +
                            "• <b>DHA (240mg por porción):</b> Ácido graso crucial para las membranas neuronales de la corteza cerebral, optimizando impulsos nerviosos.\n\n" +
                            "<b>EFECTOS SECUNDARIOS:</b> Reflujo o sabor residual a pescado si se consume con el estómago vacío.";
                    }
                    break;

                case TipoSuplemento.General:
                    statusTemporalColor = "<color=#00F3FF>[REGISTRADO]</color>";
                    ingredientePrincipal = "Suplemento General";
                    txtTitulo.text = nombreTitulo;
                    txtStatus.text = "<color=#00F3FF>PRODUCTO REGISTRADO</color>";
                    txtDosis.text = porcionDetectada;
                    txtBeneficios.text = "• Registro exitoso en historial.\n• Datos listos para comparación.\n• Revisa la tabla del menú.";
                    if (iconosSeguridad.Length > 0 && iconosSeguridad[0] != null) imgStatusIcon.sprite = iconosSeguridad[0];
                    break;
            }

            txtPorcion.text = "TAMAÑO DE SERVICIO: " + porcionDetectada.ToUpper();
            txtServicios.text = "SERVICIOS POR ENVASE: " + serviciosDetectados.ToUpper();
            txtIngredientes.text = "COMPONENTE BASE: " + ingredientePrincipal.ToUpper();

            if (esPrimerIngreso && tutorialPaso3 != null)
            {
                tutorialPaso3.transform.SetAsLastSibling();
                tutorialPaso3.SetActive(true);
                PlayerPrefs.SetInt("SafeScanTutorialContextual", 1);
                PlayerPrefs.Save();
                esPrimerIngreso = false;
            }

            if (modoRegistroActivo)
            {
                GuardarEnHistorial(ingredientePrincipal, porcionDetectada, serviciosDetectados);
            }
        }
    }

    private string ExtraerDatoDeLinea(string linea, string palabraClave)
    {
        int index = linea.IndexOf(':');
        if (index != -1 && index < linea.Length - 1)
        {
            return linea.Substring(index + 1).Trim();
        }
        return linea.Replace(palabraClave, "").Replace(":", "").Trim();
    }

    #region Persistencia de Datos

    private void GuardarEnHistorial(string ingrediente, string porcion, string servicios)
    {
        ProductoRegistrado nuevoProducto = new ProductoRegistrado
        {
            nombreUsuario = nombreTemporal,
            marcaUsuario = marcaTemporal,
            ingredienteDetectado = ingrediente,
            porcion = porcion,
            servicios = servicios,
            fechaRegistro = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            statusSeguridad = statusTemporalColor 
        };
        historialLocal.listaProductos.Add(nuevoProducto);
        PlayerPrefs.SetString("HistorialSafeScan", JsonUtility.ToJson(historialLocal));
        PlayerPrefs.Save();

        nombreTemporal = "";
        marcaTemporal = "";
        modoRegistroActivo = false;

        ActualizarListaVisualHistorial();
    }

    private void CargarHistorialDelDispositivo()
    {
        if (PlayerPrefs.HasKey("HistorialSafeScan"))
        {
            historialLocal = JsonUtility.FromJson<ListaHistorialWrapper>(PlayerPrefs.GetString("HistorialSafeScan"));
        }
        ActualizarListaVisualHistorial();
    }

    #endregion
}