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

    [SerializeField] internal SpriteRenderer sr;
    private GameManager gameManager;

    private bool isAnimating;

    public void Init(int id, Sprite front, Sprite back, GameManager gm)
    {
        CardID = id;
        frontSprite = front;
        backSprite = back;
        gameManager = gm;

        sr.sprite = backSprite;
        IsFlipped = false;
        IsMatched = false;
        transform.localScale = Vector3.one;
        gameObject.name = $"Card_{id}";
    }

    private void OnMouseDown()
    {
        if (!gameManager) return;
        if (IsMatched || IsFlipped || isAnimating) return;
        if (!gameManager.InputAllowed) return;

        // Start flip reveal coroutine; notify manager ONLY after fully shown.
        StartCoroutine(FlipRoutine(reveal: true, () =>
        {
            IsFlipped = true;
            gameManager.NotifyCardRevealed(this);
        }));
        gameManager.PlayFlipSfx();
    }

    public void MarkMatched()
    {
        IsMatched = true;
        // Optional: little scale pop
        StartCoroutine(MatchPulse());
    }

    public void ForceFlipBack()
    {
        if (!IsFlipped || IsMatched) return;
        StartCoroutine(FlipRoutine(reveal: false, () => { IsFlipped = false; }));
    }

    private IEnumerator FlipRoutine(bool reveal, System.Action onMidpoint)
    {
        isAnimating = true;
        float duration = gameManager.flipDuration;
        float half = duration * 0.5f;
        float t = 0f;

        Vector3 startScale = Vector3.one;
        // shrink X to 0
        while (t < half)
        {
            t += Time.deltaTime;
            float k = 1f - (t / half);
            transform.localScale = new Vector3(k, 1f, 1f);
            yield return null;
        }
        transform.localScale = new Vector3(0f, 1f, 1f);

        // Swap sprite
        if (reveal)
            sr.sprite = frontSprite;
        else
            sr.sprite = backSprite;

        onMidpoint?.Invoke();

        // expand X back to 1
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = (t / half);
            transform.localScale = new Vector3(k, 1f, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one;
        isAnimating = false;
    }

    private IEnumerator MatchPulse()
    {
        float time = 0f;
        float pulseTime = 0.25f;
        while (time < pulseTime)
        {
            time += Time.deltaTime;
            float s = 1f + Mathf.Sin((time / pulseTime) * Mathf.PI) * 0.15f;
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

}