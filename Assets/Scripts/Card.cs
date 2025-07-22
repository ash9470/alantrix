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
    [SerializeField]private Vector3 originalScale;  

    private void Awake()
    {
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
    }

    
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

    private IEnumerator FlipRoutine(bool reveal, System.Action onMidpoint)
    {
        isAnimating = true;
        float duration = gameManager.flipDuration;
        float half = duration * 0.5f;
        float time = 0f;

       
        while (time < half)
        {
            time += Time.deltaTime;
            float card = 1f - (time / half);
            transform.localScale = new Vector3(card * originalScale.x, originalScale.y, originalScale.z);
            yield return null;
        }
        transform.localScale = new Vector3(0f, originalScale.y, originalScale.z);

      
        if (sr) sr.sprite = reveal ? frontSprite : backSprite;
        onMidpoint?.Invoke();

      
        time = 0f;
        while (time < half)
        {
            time += Time.deltaTime;
            float Card = (time / half);
            transform.localScale = new Vector3(Card * originalScale.x, originalScale.y, originalScale.z);
            yield return null;
        }
        transform.localScale = originalScale;

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
            transform.localScale = originalScale * s;
            yield return null;
        }
        transform.localScale = originalScale;
        gameObject.SetActive(false);
    }

   
    public void RevealInstant(Sprite front)
    {
        if (sr) sr.sprite = front;
        IsFlipped = true;
    }
}