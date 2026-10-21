using System;
using System.Drawing;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mechanics
{
    public sealed class GameLogic : MonoBehaviour
    {
        [SerializeField]
        private Ball[] _balls =
        {
            new()
            {
                Color = UnityEngine.Color.white,
                Radius = 1f,
                Velocity = new Vector2(1.5f, 0f),
            },
            new()
            {
                Color = UnityEngine.Color.red,
                Radius = 2f,
                Velocity = new Vector2(-1.5f, 0f),
                Position = new Vector2(2f, 0f),
            },
        };

        private Camera _camera;
        private Transform[] _ballTransforms = null!;

        private void Start()
        {
            _ballTransforms = new Transform[_balls.Length];
            for (int i = 0; i < _balls.Length; i++)
            {
                _ballTransforms[i] = BallSpawner.Instance.SpawnBall();
            }
            _camera = Camera.main;
        }



        private void Update()
        {
            for (int i = 0; i < _balls.Length; i++)
            {
                var ball = _balls[i];
                BallHelper.ApplyMovement(ball, Time.deltaTime);
            }

            HandleWallCollisions();

            // The serialized scene may contain fewer than two balls.
            // Movement and wall response still work; pair response needs both entries.
            if (_balls.Length >= 2)
            {
                var b1 = _balls[0];
                var b2 = _balls[1];

                ref var p1 = ref b1.Position;
                ref var p2 = ref b2.Position;
                var d = p2 - p1;

                var r1 = b1.Radius;
                var r2 = b2.Radius;
                var rsum = r1 + r2;
                var dlen = d.magnitude;
                var overlap = rsum - dlen;
                if (overlap >= 0)
                {
                    ref var v1 = ref b1.Velocity;
                    ref var v2 = ref b2.Velocity;
                    var n = d / dlen;

                    var a1 = Vector2.Dot(v1, n);
                    //var a2 = -Vector2.Dot(v2, n);
                    var a2n = Vector2.Dot(v2, n);
                    //if (a1 + a2 > 0)
                    {
                        var m1 = b1.Mass;
                        var m2 = b2.Mass;
                        var ms = m1 + m2;
                        var centerV = (m1 * a1 + m2 * a2n) / ms;
                        var v1New = -a1 + 2 * centerV;
                        var v2New = -a2n + 2 * centerV;
                        v1 = v1 + (-a1 + v1New) * n;
                        v2 = v2 + (-a2n + v2New) * n;
                    }

                    {
                        var portion1 = r1 / rsum;
                        var portion2 = 1 - portion1;
                        var d1 = overlap * portion1 * -n;
                        var d2 = overlap * portion2 * n;
                        p1 += d1;
                        p2 += d2;
                    }
                }
            }

            for (int i = 0; i < _balls.Length; i++)
            {
                var ball = _balls[i];
                BallHelper.ApplyBallData(new(ball, _ballTransforms[i]));
            }
            #region Old collision check
            /*// colisions
            {

                float ballRight = _balls.Position.x + _balls.Radius;
                float rightWallX;
                {
                    var halfHeightInWorld = _camera.orthographicSize;
                    float aspectRatio = _camera.aspect;

                    rightWallX = aspectRatio * halfHeightInWorld;

                }
                if (ballRight > rightWallX)
                {
                    //collision feedback
                    ref var v = ref _balls.Velocity;
                    //v.x = -Math.Abs(v.x); same as the upper line


                    if (v.x > 0f)
                    {
                        v.x = -v.x;
                    }
                }
            }
            {
                float ballLeft = _balls.Position.x - _balls.Radius;
                float leftWallX;
                {
                    var halfHeightInWorld = _camera.orthographicSize;
                    float aspectRatio = _camera.aspect;

                    leftWallX = -aspectRatio * halfHeightInWorld;

                }
                if (ballLeft < leftWallX)
                {
                    ref var v = ref _balls.Velocity;
                    if (v.x < 0f)
                        v.x = -v.x;
                }
            }

            {
                float ballTop = _balls.Position.y + _balls.Radius;
                float topWallY;
                {
                    var CameraHeight = _camera.orthographicSize;
                    topWallY = CameraHeight;
                }

                if (ballTop > topWallY)
                {
                    ref var v = ref _balls.Velocity;

                    if (v.y > 0f)
                    {
                        v.y = -v.y;
                    }
                }
            }

            {
                float ballBottom = _balls.Position.y - _balls.Radius;
                float bottomWallY;
                {
                    var CameraHeight = _camera.orthographicSize;
                    bottomWallY = -CameraHeight;
                }

                if (ballBottom < bottomWallY)
                {
                    ref var v = ref _balls.Velocity;

                    if (v.y < 0f)
                    {
                        v.y = -v.y;
                    }
                }*/
            #endregion
        }

        private void HandleWallCollisions()
        {
            {
                var height = _camera.orthographicSize * 2;
                var width = height * _camera.aspect;
                ReadOnlySpan<Wall> walls = stackalloc Wall[4]
                {
                    new Wall(new Vector2(-width / 2, height / 2), Vector2.down),
                    new Wall(new Vector2(width / 2, height / 2), Vector2.left),
                    new Wall(new Vector2(width / 2, -height / 2), Vector2.up),
                    new Wall(new Vector2(-width / 2, -height / 2), Vector2.right),
                //(Point: new(0, height / 2), NormalTowardsScene: new Vector2(-1, -1).normalized),
                };

                for (int i = 0; i < _balls.Length; i++)
                {
                    var ball = _balls[i];
                    foreach (var wall in walls)
                    {
                        if (IsBallCollidingWithWall(ball, wall.Point, wall.NormalTowardsScene))
                        {
                            ResolveCollisionBallWall(ball, wall.NormalTowardsScene);
                        }
                    }
                }
            }
        }

        private static bool IsBallCollidingWithWall(Ball ball, Vector2 wallPoint, Vector2 wallNormalTowardScene)
        {
            Vector2 extremity = ball.Position - ball.Radius * wallNormalTowardScene;
            Vector2 diff = extremity - wallPoint;
            float dot = Vector2.Dot(diff, wallNormalTowardScene);

            return dot <= 0f ? true : false;
        }

        private static void ResolveCollisionBallWall(Ball ball, Vector2 wallNormalTowardScene)
        {
            // reflect velocity
            float speedTowardWall = -Vector2.Dot(ball.Velocity, wallNormalTowardScene);
            Vector2 v = ball.Velocity;
            if (speedTowardWall > 0f)
            {
                Vector2 reflectedVelocity = v - 2f * Vector2.Dot(v, wallNormalTowardScene) * wallNormalTowardScene;
                ball.Velocity = reflectedVelocity;
            }
        }
    }

    public readonly record struct Wall(
        Vector2 Point,
        Vector2 NormalTowardsScene);
}
