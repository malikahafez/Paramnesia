using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up (Tilt limit)")]
		public float TopClamp = 70.0f;
		[Tooltip("How far in degrees can you move the camera down (Tilt limit)")]
		public float BottomClamp = -65.0f;
		[Tooltip("Maximum horizontal pan degrees per frame to prevent camera snap")]
		public float MaxPanDeltaPerFrame = 14.0f;
		[Tooltip("Maximum vertical tilt degrees per frame to prevent camera snap")]
		public float MaxTiltDeltaPerFrame = 10.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		[HideInInspector]
		public bool JumpTriggeredThisFrame;

#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			if (_controller == null) _controller = GetComponent<CharacterController>();
			if (_input == null) _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
#endif
			GroundedCheck();
			JumpAndGravity();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			if (_verticalVelocity > 0.5f)
			{
				Grounded = false;
				return;
			}

			float checkRadius = Mathf.Min(GroundedRadius, 0.28f);
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y + checkRadius - 0.08f, transform.position.z);
			Grounded = (_controller != null && _controller.isGrounded) ||
			           Physics.CheckSphere(spherePosition, checkRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			if (_input != null && _input.look.sqrMagnitude >= _threshold && CinemachineCameraTarget != null)
			{
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

				float tiltStep = Mathf.Clamp(_input.look.y * RotationSpeed * deltaTimeMultiplier, -MaxTiltDeltaPerFrame, MaxTiltDeltaPerFrame);
				float panStep = Mathf.Clamp(_input.look.x * RotationSpeed * deltaTimeMultiplier, -MaxPanDeltaPerFrame, MaxPanDeltaPerFrame);

				_cinemachineTargetPitch += tiltStep;
				_rotationVelocity = panStep;

				// Clamp camera tilt (pitch)
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// Rotate the player horizontally (pan)
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
			else if (CinemachineCameraTarget != null)
			{
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
			}
		}

		private void Move()
		{
			float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;
			if (_input.move != Vector2.zero)
			{
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				_fallTimeoutDelta = FallTimeout;

				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
					_jumpTimeoutDelta = JumpTimeout;
					_input.jump = false;
					Grounded = false;
					JumpTriggeredThisFrame = true;
				}
				else if (_input.jump)
				{
					_input.jump = false;
				}

				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				_jumpTimeoutDelta = JumpTimeout;

				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				_input.jump = false;
			}

			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}
