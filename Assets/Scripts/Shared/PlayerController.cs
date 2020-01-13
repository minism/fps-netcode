using KinematicCharacterController;
using UnityEngine;

[RequireComponent(typeof(KinematicCharacterMotor))]
public class PlayerController :
    MonoBehaviour, ICharacterController, IPlayerController {
  [Header("Physics/Movement params")]
  public float maxSpeed = 1f;
  public float maxAirSpeed = 1f;
  public float airAccel = 1f;
  public float airDrag = 0.1f;
  public float jumpForce = 1f;
  public bool jumpWhileSliding = false;
  public float bunnyhopTimeWindow = 0.25f;

  [Header("Low level tweaks")]
  public float jumpUngroundingForce = 0.1f;

  // Components.
  private KinematicCharacterMotor motor;
  private Animator animator;

  // Jumping state.
  private struct JumpState {
    public bool queuedAttempt;
    public bool isJumping;
    public bool jumpedThisFrame;
    public float timeSinceAtttempt;
  }
  private JumpState jumpState;

  // The next movement vectors to use, computed from inputs.
  private Vector3 moveVector;
  private Vector3 lookVector;

  private void Awake() {
    motor = GetComponent<KinematicCharacterMotor>();
    motor.CharacterController = this;
    animator = GetComponentInChildren<Animator>();
  }

  // Sets the move input data to use for the next update frame.
  public void SetPlayerInputs(PlayerInputs inputs) {
    // Create a clamped movement vector to avoid the classic diagonal movement problem.            
    var inputVector = Vector3.ClampMagnitude(
        new Vector3(inputs.RightAxis, 0, inputs.ForwardAxis), 1f);

    // Determine the direction we should be moving within our plane, based on the view direction.
    var viewForward = Vector3.ProjectOnPlane(
        inputs.ViewDirection * Vector3.forward, Vector3.up).normalized;

    // TODO: Is this needeD?
    if (viewForward.sqrMagnitude == 0f) {
      viewForward = Vector3.ProjectOnPlane(
          inputs.ViewDirection * Vector3.up, Vector3.up).normalized;
    }

    // Apply view direction to input vector.
    var rotation = Quaternion.LookRotation(viewForward, Vector3.up);
    moveVector = rotation * inputVector;
    lookVector = viewForward;

    // Process jump input.
    if (inputs.Jump) {
      jumpState.queuedAttempt = true;
      jumpState.timeSinceAtttempt = 0f;
    }
  }

  private bool IsGroundJumpable() {
    return jumpWhileSliding ?
        motor.GroundingStatus.FoundAnyGround :
        motor.GroundingStatus.IsStableOnGround;
  }

  /**
   * Networking details.
   */
  public PlayerState ToNetworkState() {
    return new PlayerState();
    //return new PlayerState {
    //  SimplePosition = transform.position,
    //  MotorState = motor.GetState(),
    //};
  }

  public void ApplyNetworkState(PlayerState state) {
    //motor.ApplyState(state.MotorState);
  }

  public void Simulate(float dt) { }

  /**
   * KinematicCharacterController API.
   */

  public void UpdateRotation(ref Quaternion currentRotation, float deltaTime) {
    // TODO: Turning interpolation can go here if needed.
    if (lookVector.sqrMagnitude > 0) {
      currentRotation = Quaternion.LookRotation(lookVector, motor.CharacterUp);
    }
  }

  public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) {
    // Handle ground movement.
    if (motor.GroundingStatus.IsStableOnGround) {
      // Orient the movement vector based on the ground normal
      var right = Vector3.Cross(moveVector, motor.CharacterUp);
      var surfaceMoveVector = Vector3.Cross(motor.GroundingStatus.GroundNormal, right);

      // TODO: Velocity smoothing can go here if needed.
      var groundSpeed = maxSpeed;
      currentVelocity = surfaceMoveVector * groundSpeed;
    }
    
    // Handle air movement.
    else {
      // Apply air acceleration.
      if (moveVector.sqrMagnitude > 0f) {
        var airSpeed = maxAirSpeed;
        var targetVelocity = moveVector * airSpeed;
        var diff = Vector3.ProjectOnPlane(targetVelocity - currentVelocity, Physics.gravity);
        currentVelocity += diff * airAccel * deltaTime;
      }

      // Apply air drag.
      currentVelocity *= (1f / (1f + (airDrag * deltaTime)));

      // Apply gravity.
      currentVelocity += Physics.gravity * deltaTime;
    }

    // Process jumping.
    jumpState.jumpedThisFrame = false;
    if (jumpState.queuedAttempt && IsGroundJumpable() && !jumpState.isJumping) {
      var jumpVector = Vector3.up;

      // Force the motor to detach itself from the ground before we apply further force.
      motor.ForceUnground(jumpUngroundingForce);

      // Apply jump velocity and update state.
      currentVelocity +=
          (jumpVector * jumpForce) - Vector3.Project(currentVelocity, Vector3.up);
      jumpState.jumpedThisFrame = true;
      jumpState.isJumping = true;
      jumpState.queuedAttempt = false;
    }

    // Update animation state.
    //var animSpeed = new Vector2(currentVelocity.x, currentVelocity.z).sqrMagnitude > 0 ? 1f : 0;
    //animator.SetFloat("speed", animSpeed);
  }

  public void BeforeCharacterUpdate(float deltaTime) {
  }

  public void AfterCharacterUpdate(float deltaTime) {
    // Check resetting the jump state.
    jumpState.timeSinceAtttempt += deltaTime;
    if (jumpState.timeSinceAtttempt > bunnyhopTimeWindow) {
      jumpState.queuedAttempt = false;
    }
    if (IsGroundJumpable() && !jumpState.jumpedThisFrame) {
      jumpState.isJumping = false;
    }
  }

  public void PostGroundingUpdate(float deltaTime) {
  }

  public bool IsColliderValidForCollisions(Collider coll) {
    return true;
  }

  public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {
  }

  public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {
  }

  public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) {
  }

  public void OnDiscreteCollisionDetected(Collider hitCollider) {
  }
}