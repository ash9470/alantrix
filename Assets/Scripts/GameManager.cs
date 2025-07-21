using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;

public class GameManager : MonoBehaviour
{
    //[SerializeField] Card CardPrefab;
    //[SerializeField] Transform gridTransform;

    //[SerializeField] private Sprite[] sprites;

    //private List<Sprite> spritepairs;

    //private void PrepaiSprites()
    //{
    //    spritepairs = new List<Sprite>();

    //    for (int i = 0; i < sprites.Length; i++)
    //    {
    //        spritepairs.Add(sprites[i]);
    //        spritepairs.Add(sprites[i]);
    //    }


    //}

    //private void ShuffleSprite(List<Sprite> spriteList)
    //{
    //    for (int i = spriteList.Count - 1; i > 0; i--)
    //    {
    //        int randomIndex = UnityEngine.Random.Range(0, i + 1);

    //        Sprite tempSprite = spriteList[i];
    //        spriteList[i] = spriteList[randomIndex];
    //        spriteList[randomIndex] = tempSprite;
    //    }
    //}

    [Header("Board Config")]
    public int rows;
    public int columns;
    public Vector2 boardSize = new Vector2(8f, 6f);  // world units width x height
    public float padding = 0.2f;

    [Header("Assets")]
    public Card cardPrefab;
    public Sprite backSprite;
    public List<Sprite> frontSprites; // at least half of (rows*cols)

    [Header("Gameplay Settings")]
    public float flipDuration = 0.3f;
    public float mismatchFlipBackDelay = 0.75f;
    public int baseMatchPoints = 10;
    public int mismatchPenalty = -2;
    public int comboIncrement = 1;  // each successful chain increases multiplier
    public bool autoLoad = true;

    [Header("Audio")]
    public AudioClip flipClip;
    public AudioClip matchClip;
    public AudioClip mismatchClip;
    public AudioClip gameOverClip;

    [Header("Debug")]
    public bool clearSaveOnStart = false;

    // Public state
    public bool InputAllowed { get; private set; } = true;

    // Internal
    private readonly List<Card> cards = new List<Card>();
    private readonly Queue<Card> comparisonQueue = new Queue<Card>(); // order of revealed
    private bool comparisonWorkerRunning = false;

    private int score;
    private int combo;          // number of consecutive matches
    private bool gameFinished;

    private void Start()
    {
        if (clearSaveOnStart) SaveSystem.Clear();
        if (autoLoad && SaveSystem.HasSave())
        {
            LoadGame();
        }
        else
        {
            NewGame(rows, columns);
        }
    }

    public void NewGame(int r, int c)
    {
        rows = r;
        columns = c;
        ClearBoard();

        int total = rows * columns;
        if (total % 2 != 0)
        {
            Debug.LogWarning("Total cards must be even. Adjusting columns.");
            columns += 1;
            total = rows * columns;
        }

        // Build ID list (pairs)
        int pairCount = total / 2;
        if (frontSprites.Count < pairCount)
        {
            Debug.LogError("Not enough front sprites assigned!");
            return;
        }

        List<int> ids = new List<int>(pairCount * 2);
        for (int i = 0; i < pairCount; i++)
        {
            ids.Add(i);
            ids.Add(i);
        }

        // Shuffle
        for (int i = 0; i < ids.Count; i++)
        {
            int swap = UnityEngine.Random.Range(i, ids.Count);
            (ids[i], ids[swap]) = (ids[swap], ids[i]);
        }

        CreateAndPlaceCards(ids);
        score = 0;
        combo = 0;
        gameFinished = false;
        Persist();
    }

    private void CreateAndPlaceCards(List<int> ids)
    {
        // Compute card size
        float usableWidth = boardSize.x - padding * (columns + 1);
        float usableHeight = boardSize.y - padding * (rows + 1);
        float cardSize = Mathf.Min(usableWidth / columns, usableHeight / rows);

        Vector2 start = new Vector2(-boardSize.x * 0.5f + padding + cardSize * 0.5f,
                                    boardSize.y * 0.5f - padding - cardSize * 0.5f);

        for (int index = 0; index < ids.Count; index++)
        {
            int row = index / columns;
            int col = index % columns;

            Card card = Instantiate(cardPrefab, transform);
            card.transform.position = new Vector3(
                start.x + col * (cardSize + padding),
                start.y - row * (cardSize + padding),
                0f
            );
            float scaleFactor = cardSize / cardPrefab.sr.bounds.size.x;
            card.transform.localScale = Vector3.one * scaleFactor;

            int id = ids[index];
            card.Init(id, frontSprites[id], backSprite, this);
            cards.Add(card);
        }
    }

    private void ClearBoard()
    {
        foreach (var c in cards)
        {
            if (c) Destroy(c.gameObject);
        }
        cards.Clear();
        comparisonQueue.Clear();
        comparisonWorkerRunning = false;
    }

    public void NotifyCardRevealed(Card card)
    {
        comparisonQueue.Enqueue(card);
        TryStartComparisonWorker();
    }

    private void TryStartComparisonWorker()
    {
        if (!comparisonWorkerRunning)
        {
            StartCoroutine(ComparisonWorker());
        }
    }

    private IEnumerator ComparisonWorker()
    {
        comparisonWorkerRunning = true;
        while (comparisonQueue.Count >= 2)
        {
            // Pull earliest two *currently unreconciled* cards
            Card first = null;
            Card second = null;

            // Because player can open many, we need to pick two distinct unmatched, revealed cards
            first = DequeueNextActive();
            second = DequeueNextActive();

            if (first == null || second == null)
            {
                // Not enough valid cards, break loop.
                break;
            }

            // Compare
            if (first.CardID == second.CardID)
            {
                first.MarkMatched();
                second.MarkMatched();
                combo++;
                int comboMultiplier = 1 + (combo - 1) * comboIncrement; // e.g., 1,2,3...
                AddScore(baseMatchPoints * comboMultiplier);
                PlayMatchSfx();
            }
            else
            {
                // Mismatch: schedule both to flip back after delay while allowing further flips
                StartCoroutine(DelayedFlipBack(first, second));
                combo = 0;
                AddScore(mismatchPenalty);
                PlayMismatchSfx();
            }

            // Allow a frame so new flips can enqueue
            yield return null;
        }

        comparisonWorkerRunning = false;

        // If everything matched -> game over
        if (!gameFinished && cards.All(c => c.IsMatched))
        {
            gameFinished = true;
            PlayGameOverSfx();
        }
    }

    private Card DequeueNextActive()
    {
        while (comparisonQueue.Count > 0)
        {
            var c = comparisonQueue.Dequeue();
            if (c != null && c.IsFlipped && !c.IsMatched)
                return c;
        }
        return null;
    }

    private IEnumerator DelayedFlipBack(Card a, Card b)
    {
        yield return new WaitForSeconds(mismatchFlipBackDelay);
        a.ForceFlipBack();
        b.ForceFlipBack();
        Persist();
    }

    private void AddScore(int delta)
    {
        score += delta;
        if (score < 0) score = 0;
        Persist();
        // (Hook to UI Text or TMP if you later add one.)
        Debug.Log($"Score: {score}  Combo: {combo}");
    }

    #region Audio Helpers
    public void PlayFlipSfx() => AudioManager.Instance?.Play(flipClip);
    private void PlayMatchSfx() => AudioManager.Instance?.Play(matchClip);
    private void PlayMismatchSfx() => AudioManager.Instance?.Play(mismatchClip);
    private void PlayGameOverSfx() => AudioManager.Instance?.Play(gameOverClip);
    #endregion

    #region Save / Load
    private void Persist()
    {
        SaveData data = new SaveData
        {
            rows = rows,
            cols = columns,
            score = score,
            combo = combo,
            cardIDs = cards.Select(c => c.CardID).ToArray(),
            matched = cards.Select(c => c.IsMatched).ToArray()
        };
        SaveSystem.Save(data);
    }

    private void LoadGame()
    {
        var data = SaveSystem.Load();
        if (data == null)
        {
            NewGame(rows, columns);
            return;
        }

        rows = data.rows;
        columns = data.cols;
        ClearBoard();

        CreateAndPlaceCards(data.cardIDs.ToList());

        score = data.score;
        combo = data.combo;

        // Re-apply matched states (flip them face-up instantly)
        for (int i = 0; i < cards.Count && i < data.matched.Length; i++)
        {
            if (data.matched[i])
            {
                var card = cards[i];
                // Force reveal instantly
                var sr = card.sr;
                sr.sprite = frontSprites[card.CardID];
                typeof(Card).GetField("IsFlipped", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.SetValue(card, true);
                typeof(Card).GetField("IsMatched", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.SetValue(card, true);
                card.MarkMatched();
            }
        }
        gameFinished = cards.All(c => c.IsMatched);
        Debug.Log($"Loaded game. Score {score}, Combo {combo}");
    }
    #endregion
}


[Serializable]
public class SaveData
{
    public int rows;
    public int cols;
    public int score;
    public int combo;
    public int[] cardIDs;
    public bool[] matched;
}