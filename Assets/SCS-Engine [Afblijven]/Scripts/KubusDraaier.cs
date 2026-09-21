using UnityEngine;

public class KubusDraaier : MonoBehaviour
{
    [Tooltip("Aantal volledige rotaties per seconde.")]
    public float rotatiesPerSeconde = 2f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotatiesPerSeconde * 360f * Time.deltaTime);
    }
}
