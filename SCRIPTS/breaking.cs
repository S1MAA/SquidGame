using UnityEngine;

public class breaking : MonoBehaviour
{
    [SerializeField] GameObject intactMug;
    [SerializeField] GameObject brokenMug;

    BoxCollider bc;

    private void Awake()
    {
        intactMug.SetActive(true);
        brokenMug.SetActive(false);

        bc = GetComponent<BoxCollider>();
    }

    private void OnMouseDown()
    {
        Break();
    }

    private void Break()
    {
        intactMug.SetActive(false);
        brokenMug.SetActive(true);

        bc.enabled = false;
    }
}
