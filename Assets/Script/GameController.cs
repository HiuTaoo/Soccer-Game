using System;
using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Script
{
    public class GameController : MonoBehaviour
    {
        public static GameController Instance;
        
        [SerializeField] private Button kickButton;
        [SerializeField] private Button autoKickButton;
        [SerializeField] private Button reloadButton;
        
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private GameObject player;
        
        [Header("Ball Settings")]
        [SerializeField] private int ballCount = 5;
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private float ballSpawnHeight = 0.5f; 
        [SerializeField] private float spawnPadding = 1.0f;     
        
        [Header("Goals Settings")]
        [SerializeField] private GameObject[] goal;
        [Header("Goal Effects")]
        [SerializeField] private ParticleSystem goalParticle;
        
        [Header("Sin Arc Kick Settings")]
        [SerializeField] private float flightDuration = 1.0f; 
        [SerializeField] private float arcHeight = 3.5f;      
        
        [Header("Random Target Range")]
        [SerializeField] private float randomRangeX = 1.2f;   
        [SerializeField] private float randomRangeY = 0.8f;  
        
        private PlayerController playerController;
        private Coroutine activeKickCoroutine;
        
        public GameObject[] spawnedBall;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            if (player != null)
            {
                playerController = player.GetComponent<PlayerController>();
            }
            
            SpawnBall();
        }

        public void ToggleKickButtonUI(bool toggle)
        {
            if (kickButton != null && kickButton.gameObject.activeSelf != toggle)
            {
                kickButton.gameObject.SetActive(toggle);
            }
        }

        private void ToggleAutoKickButtonUI(bool toggle)
        {
            if (autoKickButton != null && autoKickButton.gameObject.activeSelf != toggle)
            {
                autoKickButton.gameObject.SetActive(toggle);
            }
        }

        private void OnEnable()
        {
            kickButton.onClick.AddListener(OnKickButtonClick);
            autoKickButton.onClick.AddListener(OnAutoKickButtonClick);
            reloadButton.onClick.AddListener(OnReloadButtonClick);
        }

        private void OnDisable()
        {
            kickButton.onClick.RemoveListener(OnKickButtonClick);
            autoKickButton.onClick.RemoveListener(OnAutoKickButtonClick);
            reloadButton.onClick.RemoveListener(OnReloadButtonClick);
        }

        private void OnKickButtonClick()
        {
            if (playerController == null || playerController.currentNearbyBall == null) return;

            ToggleKickButtonUI(false);

            KickBall(playerController.currentNearbyBall.gameObject);
        }

        private void OnAutoKickButtonClick()
        {
            var ball = GetFarthestBall();
            if (ball == null) return;

            ToggleAutoKickButtonUI(false);
            
            ToggleKickButtonUI(false);

            KickBall(ball);
        }

        private void OnReloadButtonClick()
        {
            RestartScene();
        }

        private void KickBall(GameObject ball)
        {
            Transform targetGoal = GetTargetGoal(ball.transform.position);

            if (targetGoal == null) return;

            if (activeKickCoroutine != null)
            {
                StopCoroutine(activeKickCoroutine);
                virtualCamera.Follow = player.transform;
            }

            float offsetX = UnityEngine.Random.Range(-randomRangeX, randomRangeX);
            float offsetY = UnityEngine.Random.Range(-randomRangeY, randomRangeY);

            Vector3 randomTargetPos = targetGoal.position 
                                      + (targetGoal.right * offsetX) 
                                      + (targetGoal.up * offsetY);

            if (randomTargetPos.y < 0.2f)
            {
                randomTargetPos.y = 0.2f;
            }

            activeKickCoroutine = StartCoroutine(MoveBallInSinArc(ball.GetComponent<Rigidbody>(), randomTargetPos, flightDuration, arcHeight));
        }

        private IEnumerator MoveBallInSinArc(Rigidbody ballRb, Vector3 targetPos, float duration, float height)
        {
            Vector3 startPos = ballRb.position;
            float elapsedTime = 0f;
            
            virtualCamera.Follow = ballRb.transform;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                Vector3 currentLinearPos = Vector3.Lerp(startPos, targetPos, t);

                float sinArc = Mathf.Sin(t * Mathf.PI) * height;
                currentLinearPos.y += sinArc;

                ballRb.position = currentLinearPos;

                ballRb.transform.Rotate(Vector3.right, 360f * (Time.deltaTime / duration), Space.Self);

                yield return null;
            }
            PlayGoalEffect(targetPos);

            ballRb.velocity = Vector3.down * 1.5f; 

            yield return new WaitForSeconds(2f);
            
            virtualCamera.Follow = player.transform;
            
            //ResetBallPosition(ballRb);

            ToggleAutoKickButtonUI(true);

            activeKickCoroutine = null;
        }

        private Transform GetTargetGoal(Vector3 ballPos)
        {
            if (goal == null || goal.Length == 0) return null;
            if (goal.Length == 1) return goal[0].transform;

            Transform bestGoal = goal[0].transform;
            float maxDot = -1f;

            foreach (var g in goal)
            {
                if (g == null) continue;

                Vector3 dirToGoal = (g.transform.position - player.transform.position).normalized;
                float dot = Vector3.Dot(player.transform.forward, dirToGoal);

                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestGoal = g.transform;
                }
            }

            return bestGoal;
        }

        private Vector3 GetRandomFieldPosition()
        {
            float minX = playerController != null ? playerController.MinX + spawnPadding : -10f;
            float maxX = playerController != null ? playerController.MaxX - spawnPadding : 10f;
            float minZ = playerController != null ? playerController.MinZ + spawnPadding : -6f;
            float maxZ = playerController != null ? playerController.MaxZ - spawnPadding : 6f;

            float randomX = UnityEngine.Random.Range(minX, maxX);
            float randomZ = UnityEngine.Random.Range(minZ, maxZ);
            float groundY = player != null ? player.transform.position.y : 0f;

            return new Vector3(randomX, ballSpawnHeight + groundY, randomZ);
        }
        
        private GameObject GetFarthestBall()
        {
            if (spawnedBall == null || spawnedBall.Length == 0 || player == null)
            {
                return null;
            }

            GameObject farthestBall = null;
            float maxDistanceSqr = -1f;
            Vector3 playerPos = player.transform.position;

            foreach (var ball in spawnedBall)
            {
                if (ball == null) continue;
                
                float distanceSqr = (ball.transform.position - playerPos).sqrMagnitude;

                if (distanceSqr > maxDistanceSqr)
                {
                    maxDistanceSqr = distanceSqr;
                    farthestBall = ball;
                }
            }

            return farthestBall;
        }

        private void ResetBallPosition(Rigidbody ballRb)
        {
            if (ballRb == null) return;

            ballRb.velocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;

            ballRb.position = GetRandomFieldPosition();
        }

        private void SpawnBall()
        {
            if (ballPrefab == null)
            {
                Debug.LogWarning("ballPrefab chưa được gán trong GameController!");
                return;
            }

            spawnedBall = new GameObject[ballCount];

            for (var i = 0; i < ballCount; i++)
            {
                Vector3 spawnPos = GetRandomFieldPosition();

                var ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
                ball.transform.localScale = new Vector3(3, 3, 3);
                ball.transform.parent = transform;
                
                spawnedBall[i] = ball;
            }
        }
        
        private void PlayGoalEffect(Vector3 position)
        {
            if (goalParticle == null) return;

            goalParticle.transform.position = position;

            goalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            goalParticle.Play();
        }

        private void RestartScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.name);
        }
    }
}