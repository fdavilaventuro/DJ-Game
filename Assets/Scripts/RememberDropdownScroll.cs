using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RememberTMPDropdownScroll : MonoBehaviour
{
    public TMP_Dropdown dropdown;
    private float savedScrollPosition = 1f; // 1 = top, 0 = bottom

    private bool dropdownOpened = false;

    void Start()
    {
        dropdown.onValueChanged.AddListener(CaptureScroll);

        // Start checking for when the dropdown opens
        StartCoroutine(WatchForOpen());
    }

    void CaptureScroll(int _)
    {
        var scroll = dropdown.template.GetComponentInChildren<ScrollRect>();
        if (scroll != null)
            savedScrollPosition = scroll.verticalNormalizedPosition;
    }

    IEnumerator WatchForOpen()
    {
        while (true)
        {
            // TMP_Dropdown shows the template by activating a child object
            bool isNowOpen = dropdown.template.gameObject.activeInHierarchy;

            if (isNowOpen && !dropdownOpened)
            {
                dropdownOpened = true;
                StartCoroutine(RestoreScroll());
            }
            else if (!isNowOpen && dropdownOpened)
            {
                dropdownOpened = false;
            }

            yield return null;
        }
    }

    IEnumerator RestoreScroll()
    {
        // Wait one frame for TMP to finish building items
        yield return null;

        var scroll = dropdown.template.GetComponentInChildren<ScrollRect>();
        if (scroll != null)
            scroll.verticalNormalizedPosition = savedScrollPosition;
    }
}
