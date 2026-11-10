using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class ConfettiUI : MonoBehaviour
{
    [SerializeField, Range(8, 96)] private int confettiCount = 36;
    [SerializeField, Min(0.1f)] private float duration = 3f;
    [SerializeField] private Vector2 sizeRange = new Vector2(5f, 12f);
    [SerializeField] private Vector2 fallSpeedRange = new Vector2(90f, 170f);
    private RectTransform[] particles;
    private Image[] images;
    private Vector2[] velocities;
    private float[] spins;
    private float elapsed;

    public void Play()
    {
        if (particles == null)
        {
            var count = Mathf.Clamp(confettiCount, 8, 96);
            particles = new RectTransform[count];
            images = new Image[count];
            velocities = new Vector2[count];
            spins = new float[count];
            for (var i = 0; i < count; i++)
            {
                var item = new GameObject("Confetti", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                item.layer = gameObject.layer;
                particles[i] = (RectTransform)item.transform;
                particles[i].SetParent(transform, false);
                images[i] = item.GetComponent<Image>();
                images[i].raycastTarget = false;
            }
        }
        var rect = ((RectTransform)transform).rect;
        for (var i = 0; i < particles.Length; i++)
        {
            particles[i].gameObject.SetActive(true);
            particles[i].anchoredPosition = new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(0f, rect.yMax));
            particles[i].sizeDelta = new Vector2(Random.Range(sizeRange.x, sizeRange.y), Random.Range(sizeRange.x, sizeRange.y));
            particles[i].localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            images[i].color = Color.HSVToRGB(Random.value, 0.65f, 1f);
            velocities[i] = new Vector2(Random.Range(-40f, 40f), -Random.Range(fallSpeedRange.x, fallSpeedRange.y));
            spins[i] = Random.Range(-160f, 160f);
        }
        elapsed = 0f;
        enabled = true;
    }

    public void StopAndReset()
    {
        enabled = false;
        ResetParticles();
    }

    private void OnDisable() => ResetParticles();

    private void ResetParticles()
    {
        elapsed = 0f;
        if (particles == null) return;
        foreach (var particle in particles)
            if (particle != null) particle.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (particles == null) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration)
        {
            StopAndReset();
            return;
        }
        var alpha = Mathf.Clamp01((duration - elapsed) / 0.5f);
        for (var i = 0; i < particles.Length; i++)
        {
            particles[i].anchoredPosition += velocities[i] * Time.unscaledDeltaTime;
            particles[i].Rotate(0f, 0f, spins[i] * Time.unscaledDeltaTime);
            var color = images[i].color;
            color.a = alpha;
            images[i].color = color;
        }
    }
}
