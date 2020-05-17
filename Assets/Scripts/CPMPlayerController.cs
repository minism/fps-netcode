using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CPMPlayerController : MonoBehaviour, IPlayerController {
  // Frame occuring factors
  public float gravity = 20.0f;
  public float friction = 6;

  /* Movement stuff */
  public float moveSpeed = 7.0f;                // Ground move speed
  public float runAcceleration = 14.0f;         // Ground accel
  public float runDeacceleration = 10.0f;       // Deacceleration that occurs when running on the ground
  public float airAcceleration = 2.0f;          // Air accel
  public float airDecceleration = 2.0f;         // Deacceleration experienced when ooposite strafing
  public float airControl = 0.3f;               // How precise air control is
  public float sideStrafeAcceleration = 50.0f;  // How fast acceleration occurs to get up to sideStrafeSpeed when
  public float sideStrafeSpeed = 1.0f;          // What the max speed to generate when side strafing
  public float jumpSpeed = 8.0f;                // The speed at which the character's up axis gains when hitting jump
  public bool holdJumpToBhop = false;           // When enabled allows player to just hold jump button to keep on bhopping

  // Lazy getter.
  private CharacterController controller {
    get {
      if (_controller == null) {
        _controller = GetComponent<CharacterController>();
      }
      return _controller;
    }
  }
  private CharacterController _controller;

  private NetworkObject networkObject {
    get {
      if (_networkObject == null) {
        _networkObject = GetComponent<NetworkObject>();
      }
      return _networkObject;
    }
  }
  private NetworkObject _networkObject;

  // Last received input.
  private PlayerInputs inputs;

  // Physics state.
  private Vector3 playerVelocity = Vector3.zero;

  // Q3: players can queue the next jump just before he hits the ground
  private bool wishJump = false;

  // TODO: Extend this
  private float attackCooldownTimer = 0f;

  private IPlayerActionHandler actionHandler;

  private void Update() {
    // The CPM controller is simulated manually via Simulate(), for now this method does nothing.
    return;
  }

  /**
   * IPlayerController interface
   */

  public void SetPlayerActionHandler(IPlayerActionHandler handler) {
    this.actionHandler = handler;
  }

  public void SetPlayerInputs(PlayerInputs inputs) {
    this.inputs = inputs;
  }

  public void Simulate(float dt) {
    // Set orientation based on the view direction.
    transform.rotation = Quaternion.Euler(0, inputs.ViewDirection.eulerAngles.y, 0);

    // Process movement.
    QueueJump();
    if (controller.isGrounded)
      GroundMove(dt);
    else if (!controller.isGrounded)
      AirMove(dt);

    // Apply the final velocity to the character controller.
    controller.Move(playerVelocity * dt);

    // Process attacks.
    attackCooldownTimer -= dt;
    if (inputs.Fire && attackCooldownTimer <= 0) {
      attackCooldownTimer = 1f;
      actionHandler.CreatePlayerAttack(
          NetworkObjectType.HITSCAN_ATTACK, transform.position, inputs.ViewDirection);
    }
  }

  public PlayerState ToNetworkState() {
    return new PlayerState {
      NetworkId = networkObject.NetworkId,
      Position = transform.position,
      Rotation = transform.rotation,
      Velocity = playerVelocity,
      Grounded = controller.isGrounded,
    };
  }

  public void ApplyNetworkState(PlayerState state) {
    // The built-in CharacterController is an absolute dumbass, it caches its position in private
    // state and uses it for interpolation in .Move().  That means we cant actually serialize
    // its state over the network.
    // A shitty hack for now is to trigger OnEnable for the component, which copies
    // transform.position into its cache.
    controller.enabled = false;
    transform.position = state.Position;
    transform.rotation = state.Rotation;
    controller.enabled = true;
    playerVelocity = state.Velocity;
  }

  /*****************************************************
   * Code below is largely unmodified from CPMPlayer.cs.
   */

  /**
   * Queues the next jump just like in Q3
   */
  private void QueueJump() {
    wishJump = inputs.Jump;
  }

  /**
   * Called every frame when the engine detects that the player is on the ground
   */
  private void GroundMove(float dt) {
    Vector3 wishdir;

    // Do not apply friction if the player is queueing up the next jump
    if (!wishJump)
      ApplyFriction(1.0f, dt);
    else
      ApplyFriction(0, dt);

    wishdir = new Vector3(inputs.RightAxis, 0, inputs.ForwardAxis);
    wishdir = transform.TransformDirection(wishdir);
    wishdir.Normalize();

    var wishspeed = wishdir.magnitude;
    wishspeed *= moveSpeed;

    Accelerate(wishdir, wishspeed, runAcceleration, dt);

    // Reset the gravity velocity
    playerVelocity.y = -gravity * dt;

    if (wishJump) {
      playerVelocity.y = jumpSpeed;
      wishJump = false;
    }
  }

  /**
   * Execs when the player is in the air
  */
  private void AirMove(float dt) {
    Vector3 wishdir;
    float wishvel = airAcceleration;
    float accel;

    wishdir = new Vector3(inputs.RightAxis, 0, inputs.ForwardAxis);
    wishdir = transform.TransformDirection(wishdir);

    float wishspeed = wishdir.magnitude;
    wishspeed *= moveSpeed;

    wishdir.Normalize();

    // CPM: Aircontrol
    float wishspeed2 = wishspeed;
    if (Vector3.Dot(playerVelocity, wishdir) < 0)
      accel = airDecceleration;
    else
      accel = airAcceleration;
    // If the player is ONLY strafing left or right
    if (inputs.ForwardAxis == 0 && inputs.RightAxis != 0) {
      if (wishspeed > sideStrafeSpeed)
        wishspeed = sideStrafeSpeed;
      accel = sideStrafeAcceleration;
    }

    Accelerate(wishdir, wishspeed, accel, dt);
    if (airControl > 0)
      AirControl(wishdir, wishspeed2, dt);
    // !CPM: Aircontrol

    // Apply gravity
    playerVelocity.y -= gravity * dt;
  }

  /**
   * Air control occurs when the player is in the air, it allows
   * players to move side to side much faster rather than being
   * 'sluggish' when it comes to cornering.
   */
  private void AirControl(Vector3 wishdir, float wishspeed, float dt) {
    float zspeed;
    float speed;
    float dot;
    float k;

    // Can't control movement if not moving forward or backward
    if (Mathf.Abs(inputs.ForwardAxis) < 0.001 || Mathf.Abs(wishspeed) < 0.001)
      return;
    zspeed = playerVelocity.y;
    playerVelocity.y = 0;
    /* Next two lines are equivalent to idTech's VectorNormalize() */
    speed = playerVelocity.magnitude;
    playerVelocity.Normalize();

    dot = Vector3.Dot(playerVelocity, wishdir);
    k = 32;
    k *= airControl * dot * dot * dt;

    // Change direction while slowing down
    if (dot > 0) {
      playerVelocity.x = playerVelocity.x * speed + wishdir.x * k;
      playerVelocity.y = playerVelocity.y * speed + wishdir.y * k;
      playerVelocity.z = playerVelocity.z * speed + wishdir.z * k;

      playerVelocity.Normalize();
    }

    playerVelocity.x *= speed;
    playerVelocity.y = zspeed; // Note this line
    playerVelocity.z *= speed;
  }

  private void Accelerate(Vector3 wishdir, float wishspeed, float accel, float dt) {
    float addspeed;
    float accelspeed;
    float currentspeed;

    currentspeed = Vector3.Dot(playerVelocity, wishdir);
    addspeed = wishspeed - currentspeed;
    if (addspeed <= 0)
      return;
    accelspeed = accel * dt * wishspeed;
    if (accelspeed > addspeed)
      accelspeed = addspeed;

    playerVelocity.x += accelspeed * wishdir.x;
    playerVelocity.z += accelspeed * wishdir.z;
  }

  /**
   * Applies friction to the player, called in both the air and on the ground
   */
  private void ApplyFriction(float t, float dt) {
    Vector3 vec = playerVelocity; // Equivalent to: VectorCopy();
    float speed;
    float newspeed;
    float control;
    float drop;

    vec.y = 0.0f;
    speed = vec.magnitude;
    drop = 0.0f;

    /* Only if the player is on the ground then apply friction */
    if (controller.isGrounded) {
      control = speed < runDeacceleration ? runDeacceleration : speed;
      drop = control * friction * dt * t;
    }

    newspeed = speed - drop;
    if (newspeed < 0)
      newspeed = 0;
    if (speed > 0)
      newspeed /= speed;

    playerVelocity.x *= newspeed;
    playerVelocity.z *= newspeed;
  }
}