using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    public int CardID { get; private set; }
    public bool IsFlipped { get; private set; }
    public bool IsMatched { get; private set; }

    [SerializeField] private Sprite frontSprite;
    [SerializeField] private Sprite backSprite;

    [SerializeField] internal SpriteRenderer sr;   // assign in Inspector OR auto-grab in Awake
    private GameManager gameManager;

    private bool isAnimating;
    [SerializeField]private Vector3 originalScale;  // scale applied by GameManager when laying out board

    private void Awake()
    {
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// Called by GameManager after the card has been instantiated *and scaled/positioned*.
    /// Captures the externally-set scale so flip animation can respect it.
    /// </summary>
    public void Init(int id, Sprite front, Sprite back, GameManager gm, float scale)
    {
        CardID = id;
        frontSprite = front;
        backSprite = back;
        gameManager = gm;

        if (sr) sr.sprite = backSprite;

        IsFlipped = false;
        IsMatched = false;
        gameObject.name = $"Card_{id}";

        // Capture current (already scaled) transform scale as our base animation scale.
        originalScale.x = scale;
        originalScale.y = scale;
        originalScale.z = scale;
    }

    private void OnMouseDown()
    {
        if (!gameManager) return;
        if (IsMatched || IsFlipped || isAnimating) return;
        if (!gameManager.InputAllowed) return;

        StartCoroutine(FlipRoutine(true, () =>
        {
            IsFlipped = true;
            gameManager.NotifyCardRevealed(this);
        }));
        gameManager.PlayFlipSfx();
    }

    public void MarkMatched()
    {
        IsMatched = true;
        StartCoroutine(MatchPulse());
    }

    public void ForceFlipBack()
    {
        if (!IsFlipped || IsMatched) return;
        StartCoroutine(FlipRoutine(false, () => { IsFlipped = false; }));
    }

    /// <summary>
    /// Flip animation that preserves original (GameManager) scale.
    /// </summary>
    private IEnumerator FlipRoutine(bool reveal, System.Action onMidpoint)
    {
        isAnimating = true;
        float duration = gameManager.flipDuration;
        float half = duration * 0.5f;
        float t = 0f;

        // shrink X to 0
        while (t < half)
        {
            t += Time.deltaTime;
            float k = 1f - (t / half);
            transform.localScale = new Vector3(k * originalScale.x, originalScale.y, originalScale.z);
            yield return null;
        }
        transform.localScale = new Vector3(0f, originalScale.y, originalScale.z);

        // swap sprite
        if (sr) sr.sprite = reveal ? frontSprite : backSprite;
        onMidpoint?.Invoke();

        // expand X back to original
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = (t / half);
            transform.localScale = new Vector3(k * originalScale.x, originalScale.y, originalScale.z);
            yield return null;
        }
        transform.localScale = originalScale;

        isAnimating = false;
    }

    /// <summary>
    /// Small 'pop' on match, then disable (optional).
    /// </summary>
    private IEnumerator MatchPulse()
    {
        float time = 0f;
        float pulseTime = 0.25f;
        while (time < pulseTime)
        {
            time += Time.deltaTime;
            float s = 1f + Mathf.Sin((time / pulseTime) * Mathf.PI) * 0.15f;
            transform.localScale = originalScale * s;
            yield return null;
        }
        transform.localScale = originalScale;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Used by GameManager when loading a save to reveal matched cards instantly (no animation).
    /// </summary>
    public void RevealInstant(Sprite front)
    {
        if (sr) sr.sprite = front;
        IsFlipped = true;
    }
}