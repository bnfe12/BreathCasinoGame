using BreathCasino.Systems;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BreathCasino.Gameplay
{
    public sealed class BCMenuSoundHooks : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            BCAudioManager.Instance?.PlayUIHover();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            BCAudioManager.Instance?.PlayUIClick();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            BCAudioManager.Instance?.PlayUIClick();
        }
    }
}
