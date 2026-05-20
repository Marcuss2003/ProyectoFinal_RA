using UnityEngine;

public class SafeScanMirror : MonoBehaviour
{
    [Header("Referencia al Panel Original (De la Creatina)")]
    [SerializeField] private GameObject panelOriginal; 

    void Update()
    {
        if (panelOriginal != null)
        {
            // El clon copia exactamente el estado del original en cada frame
            if (gameObject.activeSelf != panelOriginal.activeSelf)
            {
                gameObject.SetActive(panelOriginal.activeSelf);
            }
        }
    }
}