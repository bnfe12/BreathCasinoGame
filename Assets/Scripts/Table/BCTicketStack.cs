using UnityEngine;
using BreathCasino.Core;
using System.Collections.Generic;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Управляет стопкой физических билетов на столе.
    /// Билеты лежат друг на друге с небольшим смещением.
    /// </summary>
    public class BCTicketStack : MonoBehaviour
    {
        [Header("Stack Settings")]
        [SerializeField] private float stackOffset = 0.002f; // Смещение между билетами
        
        private List<BCTicketDisplay> _tickets = new List<BCTicketDisplay>();
        private BCTicketDisplay _ticketInHand;

        public int TicketCount => _tickets.Count;
        public bool HasTicketInHand => _ticketInHand != null;
        public BCTicketDisplay TicketInHand => _ticketInHand;
        public int PendingUnclaimedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _tickets.Count; i++)
                {
                    if (_tickets[i] != null && !_tickets[i].ClaimedToInventory)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void AddTicket(BCTicketDisplay ticket)
        {
            if (ticket == null) return;

            _tickets.Add(ticket);
            ticket.transform.SetParent(transform, false);
            UpdateTicketPosition(ticket, _tickets.Count - 1);
            
            Debug.Log($"[TicketStack] Added {ticket.TicketName}, total: {_tickets.Count}");
        }

        public BCTicketDisplay TakeTopTicket(Transform handHolder)
        {
            if (_tickets.Count == 0 || _ticketInHand != null) return null;

            BCTicketDisplay topTicket = _tickets[_tickets.Count - 1];
            _tickets.RemoveAt(_tickets.Count - 1);
            
            topTicket.TakeToHand(handHolder);
            _ticketInHand = topTicket;
            
            Debug.Log($"[TicketStack] Took {topTicket.TicketName} to hand, remaining: {_tickets.Count}");
            return topTicket;
        }

        public BCTicketDisplay PopTopTicket()
        {
            if (_tickets.Count == 0 || _ticketInHand != null)
            {
                return null;
            }

            BCTicketDisplay topTicket = _tickets[_tickets.Count - 1];
            _tickets.RemoveAt(_tickets.Count - 1);
            return topTicket;
        }

        public void ReturnTicketToTop()
        {
            if (_ticketInHand == null) return;

            _tickets.Add(_ticketInHand);
            int index = _tickets.Count - 1;
            
            Vector3 localPos = new Vector3(0, index * stackOffset, 0);
            _ticketInHand.ReturnToStack(transform, localPos, Quaternion.identity);
            
            Debug.Log($"[TicketStack] Returned {_ticketInHand.TicketName} to top, total: {_tickets.Count}");
            _ticketInHand = null;
        }

        public void RemoveTicketInHand()
        {
            if (_ticketInHand == null) return;

            Debug.Log($"[TicketStack] Removed {_ticketInHand.TicketName} from game");
            Destroy(_ticketInHand.gameObject);
            _ticketInHand = null;
        }

        private void UpdateTicketPosition(BCTicketDisplay ticket, int index)
        {
            ticket.transform.localPosition = new Vector3(0, index * stackOffset, 0);
            ticket.transform.localRotation = Quaternion.identity;
        }

        public void Clear()
        {
            foreach (var ticket in _tickets)
            {
                if (ticket != null) Destroy(ticket.gameObject);
            }
            _tickets.Clear();
            
            if (_ticketInHand != null)
            {
                Destroy(_ticketInHand.gameObject);
                _ticketInHand = null;
            }
        }
    }
}
