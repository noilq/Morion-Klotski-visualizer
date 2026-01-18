using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ErrorPopup : MonoBehaviour
{
    public static ErrorPopup Instance;

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panel != null)
            panel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;

        if (panel != null)
            panel.SetActive(true);
    }

    private void Close()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
