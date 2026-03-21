using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;

    private float elapsed;
    private bool running;

    public float Elapsed => elapsed;

    public void StartTimer()
    {
        elapsed = 0f;
        running = true;
    }

    public void StopTimer()
    {
        running = false;
    }

    private void Update()
    {
        if (!running) return;

        elapsed += Time.deltaTime;
        UpdateDisplay(elapsed);
    }

    private void UpdateDisplay(float seconds)
    {
        if (timerText == null) return;

        int min = (int)(seconds / 60);
        int sec = (int)(seconds % 60);
        int ms  = (int)((seconds * 100) % 100);
        timerText.text = $"{min:00}:{sec:00}.{ms:00}";
    }

    /// <summary>최종 시간을 포맷된 문자열로 반환</summary>
    public string GetFormattedTime()
    {
        int min = (int)(elapsed / 60);
        int sec = (int)(elapsed % 60);
        int ms  = (int)((elapsed * 100) % 100);
        return $"{min:00}:{sec:00}.{ms:00}";
    }
}
