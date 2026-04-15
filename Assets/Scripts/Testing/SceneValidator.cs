using System.Collections.Generic;
using BreathCasino.Core;
using System.Text;
using UnityEngine;

namespace BreathCasino.Core
{
    public class SceneValidator : MonoBehaviour
    {
        public bool LastValidationPassed { get; private set; }

        public bool ValidateAndReport(SceneBootstrap bootstrap)
        {
            List<string> errors = new();

            if (bootstrap == null)
            {
                errors.Add("Bootstrap is missing.");
            }
            else
            {
                Require(bootstrap.mainCamera != null, "Main Camera reference is missing.", errors);
                Require(bootstrap.managersRoot != null, "Managers root is missing.", errors);
                Require(bootstrap.tableRoot != null, "Table root is missing.", errors);
                Require(bootstrap.playerRoot != null, "Player root is missing.", errors);
                Require(bootstrap.enemyRoot != null, "Enemy root is missing.", errors);
                Require(bootstrap.gunRoot != null, "Gun root is missing.", errors);
                Require(bootstrap.uiRoot != null, "UI root is missing.", errors);
                Require(bootstrap.playerMainSlot != null, "Player main slot is missing.", errors);
                Require(bootstrap.enemyMainSlot != null, "Enemy main slot is missing.", errors);
                Require(bootstrap.gunSpot != null, "Gun spot is missing.", errors);
                Require(bootstrap.bulletSpot != null, "Bullet spot is missing.", errors);
                Require(bootstrap.playerWeaponHolder != null, "Player weapon holder is missing.", errors);
                Require(bootstrap.enemyWeaponHolder != null, "Enemy weapon holder is missing.", errors);
                Require(bootstrap.playerHandHolder != null, "Player hand holder is missing.", errors);
                Require(bootstrap.enemyHandHolder != null, "Enemy hand holder is missing.", errors);
                Require(bootstrap.ticketManager != null, "TicketManager is missing.", errors);
                Require(bootstrap.cardManager != null, "BCCardManager is missing.", errors);
                Require(bootstrap.gameManager != null, "GameManager is missing.", errors);
                // statusText — опциональный debug HUD, не блокирует игру
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            int activeCameras = 0;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].isActiveAndEnabled)
                {
                    activeCameras++;
                }
            }

            if (activeCameras != 1)
            {
                errors.Add($"Expected exactly 1 active camera, found {activeCameras}.");
            }

            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            if (lights.Length == 0)
            {
                errors.Add("No Light found in scene.");
            }

            LastValidationPassed = errors.Count == 0;

            if (LastValidationPassed)
            {
                Debug.Log("Scene validation passed.");
                return true;
            }

            StringBuilder builder = new();
            builder.AppendLine("Scene validation failed:");
            for (int i = 0; i < errors.Count; i++)
            {
                builder.AppendLine($"- {errors[i]}");
            }

            Debug.LogWarning(builder.ToString());
            return false;
        }

        private static void Require(bool condition, string message, List<string> errors)
        {
            if (!condition)
            {
                errors.Add(message);
            }
        }
    }
}
