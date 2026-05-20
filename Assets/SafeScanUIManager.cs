using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SafeScanUIManager : MonoBehaviour
{
    [Header("Componentes de Interfaz")]
    [SerializeField] private TextMeshProUGUI txtPorcentaje;
    [SerializeField] private Image imgDialCircular; // Se puede dejar vacío si usas la esfera 3D

    /// <summary>
    /// Actualiza el progreso en pantalla. 
    /// </summary>
    /// <param name="progreso">Valor normalizado entre 0f y 1f</param>
    public void ActualizarProgreso(float progreso)
    {
        // Aseguramos que el valor no se salga de los límites de 0 y 1
        progreso = Mathf.Clamp01(progreso);

        // 1. Actualizamos el texto convirtiendo el valor (0-1) a un entero (0-100)
        if (txtPorcentaje != null)
        {
            int porcentajeEntero = Mathf.RoundToInt(progreso * 100f);
            txtPorcentaje.text = porcentajeEntero + "%";
        }

        // 2. Si en algún momento decides usar una imagen de dial tipo Filled
        if (imgDialCircular != null)
        {
            imgDialCircular.fillAmount = progreso;
        }
    }
}
