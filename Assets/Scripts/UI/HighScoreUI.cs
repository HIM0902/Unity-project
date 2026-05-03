using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Attach to the root of your high-score UI panel.
///
/// HIDDEN BY DEFAULT: the panel's GameObject starts inactive and is only
/// revealed by ExtractionPoint calling Show() after the score is recorded.
///
/// AUTO-ASSIGN: Awake() finds all TextMeshProUGUI children in hierarchy order
/// and treats them as score rows (index 0 = 1st place). No Inspector wiring needed.
/// </summary>
public class HighScoreUI : MonoBehaviour
{
    [Header("Format")]
    [Tooltip("{0} = rank (1-based), {1} = score value")]
    [SerializeField] private string rowFormat = "{0}.  {1:N0}";

    [Tooltip("Shown for slots that have no real score yet")]
    [SerializeField] private string emptyPlaceholder = "---";

    [Header("New-entry highlight")]
    [Tooltip("Colour flashed on the row that just entered the board")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.1f); // gold
    [SerializeField] private float highlightDuration = 1.5f;

    private TextMeshProUGUI[] _scoreTexts;
    private Color[]           _defaultColors;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        AutoAssignTexts();

        // Visibility is controlled by HighScorePanel (the root).
        // Do not hide/show this child object directly.
    }

    private void OnEnable()
    {
        // Refresh every time the panel becomes visible
        Refresh();
    }

    // -----------------------------------------------------------------------
    // Public API — called by ExtractionPoint
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reveal the panel and populate it with the current top scores.
    /// Pass the 0-based rank returned by HighScoreManager.SubmitScore to
    /// highlight the row that just entered or moved up the board.
    /// </summary>
    public void Show(int highlightRank = -1)
    {
        // HighScorePanel root is activated by ExtractionPoint before this is called.
        // We just refresh and highlight.
        Refresh();

        if (highlightRank >= 0 && highlightRank < _scoreTexts.Length)
            StartCoroutine(FlashRow(highlightRank));
    }

    /// <summary>Rebuild all rows from HighScoreManager data.</summary>
    public void Refresh()
    {
        if (HighScoreManager.Instance == null)
        {
            Debug.LogWarning("HighScoreUI: No HighScoreManager found in scene.");
            return;
        }

        if (_scoreTexts == null || _scoreTexts.Length == 0)
            AutoAssignTexts();

        IReadOnlyList<int> scores = HighScoreManager.Instance.GetTopScores();

        for (int i = 0; i < _scoreTexts.Length; i++)
        {
            if (_scoreTexts[i] == null) continue;

            bool hasScore = i < scores.Count && scores[i] > 0;
            _scoreTexts[i].text = hasScore
                ? string.Format(rowFormat, i + 1, scores[i])
                : $"{i + 1}.  {emptyPlaceholder}";
        }
    }

    // -----------------------------------------------------------------------
    // Auto-assign
    // -----------------------------------------------------------------------

    private void AutoAssignTexts()
    {
        _scoreTexts    = GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        _defaultColors = new Color[_scoreTexts.Length];

        for (int i = 0; i < _scoreTexts.Length; i++)
            _defaultColors[i] = _scoreTexts[i].color;

        if (_scoreTexts.Length == 0)
            Debug.LogWarning("HighScoreUI: No TextMeshProUGUI children found.");
        else
            Debug.Log($"HighScoreUI: Auto-assigned {_scoreTexts.Length} score row(s).");
    }

    // -----------------------------------------------------------------------
    // Highlight coroutine
    // -----------------------------------------------------------------------

    private IEnumerator FlashRow(int rowIndex)
    {
        TextMeshProUGUI label  = _scoreTexts[rowIndex];
        Color           origin = _defaultColors[rowIndex];
        float           t      = 0f;

        // Fade in
        while (t < highlightDuration * 0.3f)
        {
            t += Time.deltaTime;
            label.color = Color.Lerp(origin, highlightColor, t / (highlightDuration * 0.3f));
            yield return null;
        }

        label.color = highlightColor;
        t = 0f;

        // Hold
        yield return new WaitForSeconds(highlightDuration * 0.4f);

        // Fade out
        while (t < highlightDuration * 0.3f)
        {
            t += Time.deltaTime;
            label.color = Color.Lerp(highlightColor, origin, t / (highlightDuration * 0.3f));
            yield return null;
        }

        label.color = origin;
    }
}