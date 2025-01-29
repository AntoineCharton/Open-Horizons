using TMPro;
using UnityEngine;

public class IntDisplay : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI _textMeshProUGUI;
    private int _cachedValue;
    [SerializeField] private bool _interpolate;
    
    public void SetInt(int value)
    {
        if(_interpolate)
            _cachedValue = (int) Mathf.Lerp(_cachedValue, value, Time.deltaTime * 10);
        else
        {
            _cachedValue = value;
        }
        _textMeshProUGUI.text = _cachedValue.ToString();
    }
}
