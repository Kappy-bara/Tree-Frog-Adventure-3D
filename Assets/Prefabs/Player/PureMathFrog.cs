using UnityEngine;

public class PureMathFrog : MonoBehaviour
{
    [Header("Procedural Aim (Head)")]
    public Transform headBone;
    public Camera mainCamera;
    public LayerMask environmentLayer = ~0;
    public float maxLookAngleHorizontal = 85f;
    public float maxLookAngleVertical = 40f;
    public float lookSpeed = 10f;
    public Vector3 headOffsetEuler = new Vector3(0, 0, 0);

    [Header("Procedural Breathing (Torso)")]
    public Transform frogBody; // Spine02
    public float breatheSpeed = 3f;
    public float breatheAmount = 0.03f;
    [Tooltip("How much the chest naturally tilts back as lungs fill.")]
    public float breatheChestTilt = 2f;
    private Vector3 originalBodyScale;
    private Quaternion originalBodyRot;

    [Header("Procedural Skeleton Root (Hips/Armature)")]
    public Transform flipRootBone;
    private Quaternion flipRootInitRot;
    private Vector3 flipRootInitPos;

    [Header("Procedural Limbs (References)")]
    public Transform leftUpLeg;
    public Transform rightUpLeg;
    public Transform leftLowerLeg;
    public Transform rightLowerLeg;
    public Transform leftArm;
    public Transform rightArm;

    [Header("Procedural Limbs (Axes)")]
    public Vector3 legSwingAxis = new Vector3(1, 0, 0);
    public Vector3 armSwingAxis = new Vector3(1, 0, 0);
    [Tooltip("The axis the spine twists around (usually Y or Z).")]
    public Vector3 spineTwistAxis = new Vector3(0, 1, 0);
    [Tooltip("The axis the hips sway side-to-side on (usually Z or Y).")]
    public Vector3 spineSwayAxis = new Vector3(0, 0, 1);

    [Header("Human Walk Settings")]
    public float walkAnimationSpeed = 1.5f;
    public float walkStrideAngle = 45f;
    public float kneeBendAngle = 60f;
    public float armSwingAngle = 40f;
    public float bodyBobAmount = 0.15f;
    [Tooltip("How much the torso twists to counterbalance the swinging arms.")]
    public float spineTwistAmount = 15f;
    [Tooltip("How much the body leans side-to-side over the planted foot.")]
    public float weightShiftAmount = 5f;

    [Header("Dynamic Jump Settings")]
    [Tooltip("Max leg stretch when falling at high speeds.")]
    public float maxFallStretch = 50f;
    [Tooltip("How tightly the legs tuck when at the apex of the jump.")]
    public float apexTuckAngle = -20f;
    public float limbBlendSpeed = 15f;

    [Header("Wall Jump Settings")]
    [Tooltip("Arm angle while braced against a wall.")]
    public float wallBraceArmAngle = -70f;
    [Tooltip("How much the spine leans toward the wall while sliding.")]
    public float wallLeanAmount = 15f;
    [Tooltip("How long the push-off pose takes to fully extend after a wall jump.")]
    public float wallJumpPushDuration = 0.3f;

    // Bone initial state memory
    private Quaternion leftUpLegInit, rightUpLegInit;
    private Quaternion leftLowerLegInit, rightLowerLegInit;
    private Quaternion leftArmInit, rightArmInit;

    // Velocity tracking & Phase memory
    private Vector3 lastPosition;
    private Vector3 velocity;
    private float smoothedHorizontalSpeed;
    private float walkCyclePhase = 0f;

    // Double jump flip state
    private bool isDoingDoubleJumpFlip = false;
    private float flipTimer = 0f;
    private float flipDuration = 0.5f;

    // Wall slide / wall jump state
    private bool isWallSliding = false;
    private Vector3 wallSlideNormal;
    private bool isDoingWallJumpPush = false;
    private float wallJumpPushTimer = 0f;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (frogBody != null)
        {
            originalBodyScale = frogBody.localScale;
            originalBodyRot = frogBody.localRotation;
        }

        if (flipRootBone != null)
        {
            flipRootInitRot = flipRootBone.localRotation;
            flipRootInitPos = flipRootBone.localPosition;
        }

        lastPosition = transform.position;

        if (leftUpLeg != null) leftUpLegInit = leftUpLeg.localRotation;
        if (rightUpLeg != null) rightUpLegInit = rightUpLeg.localRotation;
        if (leftLowerLeg != null) leftLowerLegInit = leftLowerLeg.localRotation;
        if (rightLowerLeg != null) rightLowerLegInit = rightLowerLeg.localRotation;
        if (leftArm != null) leftArmInit = leftArm.localRotation;
        if (rightArm != null) rightArmInit = rightArm.localRotation;
    }

    void LateUpdate()
    {
        CalculateVelocity();
        HandleProceduralLimbsAndBob();
        HandleProceduralAim(); // Aim is called after body twisting to ensure the head stays locked on target!
        HandleDoubleJumpFlip();
        HandleWallJumpPush();
    }

    private void CalculateVelocity()
    {
        velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;

        float rawHorizontal = new Vector3(velocity.x, 0, velocity.z).magnitude;
        smoothedHorizontalSpeed = Mathf.Lerp(smoothedHorizontalSpeed, rawHorizontal, Time.deltaTime * 15f);
    }

    private void HandleProceduralLimbsAndBob()
    {
        float verticalSpeed = velocity.y;

        Quaternion leftUpTarget = leftUpLegInit;
        Quaternion rightUpTarget = rightUpLegInit;
        Quaternion leftLowerTarget = leftLowerLegInit;
        Quaternion rightLowerTarget = rightLowerLegInit;
        Quaternion leftArmTarget = leftArmInit;
        Quaternion rightArmTarget = rightArmInit;
        Quaternion spineTarget = originalBodyRot;
        Vector3 rootPosTarget = flipRootInitPos;

        // 0a. WALL SLIDE BRACE
        if (isWallSliding && !isDoingDoubleJumpFlip && !isDoingWallJumpPush)
        {
            if (leftArm != null) leftArmTarget = leftArmInit * Quaternion.AngleAxis(wallBraceArmAngle, armSwingAxis);
            if (rightArm != null) rightArmTarget = rightArmInit * Quaternion.AngleAxis(wallBraceArmAngle, armSwingAxis);

            leftUpTarget = leftUpLegInit * Quaternion.AngleAxis(apexTuckAngle, legSwingAxis);
            rightUpTarget = rightUpLegInit * Quaternion.AngleAxis(apexTuckAngle, legSwingAxis);

            float wallLeanSign = Mathf.Sign(transform.InverseTransformDirection(wallSlideNormal).x);
            if (wallLeanSign == 0f) wallLeanSign = 1f;
            spineTarget = originalBodyRot * Quaternion.AngleAxis(wallLeanSign * wallLeanAmount, spineSwayAxis);
        }
        // 1. DYNAMIC JUMPING & FALLING
        else if (Mathf.Abs(verticalSpeed) > 1.5f && !isDoingDoubleJumpFlip)
        {
            // Dynamically blend between a tight tuck at the jump apex and a long stretch when falling fast
            float normalizedVertical = Mathf.Clamp(verticalSpeed / 10f, -1f, 1f);
            float stretch = Mathf.Lerp(maxFallStretch, apexTuckAngle, (normalizedVertical + 1f) / 2f);

            leftUpTarget = leftUpLegInit * Quaternion.AngleAxis(stretch, legSwingAxis);
            rightUpTarget = rightUpLegInit * Quaternion.AngleAxis(stretch, legSwingAxis);
            if (leftLowerLeg != null) leftLowerTarget = leftLowerLegInit * Quaternion.AngleAxis(stretch * 0.3f, legSwingAxis);
            if (rightLowerLeg != null) rightLowerTarget = rightLowerLegInit * Quaternion.AngleAxis(stretch * 0.3f, legSwingAxis);

            if (leftArm != null) leftArmTarget = leftArmInit * Quaternion.AngleAxis(-stretch * 0.8f, armSwingAxis);
            if (rightArm != null) rightArmTarget = rightArmInit * Quaternion.AngleAxis(-stretch * 0.8f, armSwingAxis);
        }
        // 2. WALKING
        else if (smoothedHorizontalSpeed > 0.1f && !isDoingDoubleJumpFlip)
        {
            float speedMultiplier = Mathf.Clamp(smoothedHorizontalSpeed * 0.2f, 0.5f, 2f);
            walkCyclePhase += speedMultiplier * walkAnimationSpeed * Time.deltaTime;
            walkCyclePhase %= 1.0f;

            float leftPhase = walkCyclePhase;
            float rightPhase = (walkCyclePhase + 0.5f) % 1.0f;

            // Leg Pendulums
            float leftThigh = -Mathf.Cos(leftPhase * Mathf.PI * 2f) * walkStrideAngle;
            float rightThigh = -Mathf.Cos(rightPhase * Mathf.PI * 2f) * walkStrideAngle;

            // Knee Snapping
            float leftKnee = (leftPhase < 0.5f) ? Mathf.Sin(leftPhase * 2f * Mathf.PI) * kneeBendAngle : 0f;
            float rightKnee = (rightPhase < 0.5f) ? Mathf.Sin(rightPhase * 2f * Mathf.PI) * kneeBendAngle : 0f;

            // Arm Swinging
            float leftArmRot = -Mathf.Cos(rightPhase * Mathf.PI * 2f) * armSwingAngle;
            float rightArmRot = -Mathf.Cos(leftPhase * Mathf.PI * 2f) * armSwingAngle;

            // NEW: Torso Weight Shifting & Twisting
            // The spine twists counter to the legs to maintain balance
            float twistOffset = Mathf.Cos(walkCyclePhase * Mathf.PI * 2f) * spineTwistAmount;
            // The hips lean left/right over whichever foot is currently planted
            float swayOffset = Mathf.Sin(walkCyclePhase * Mathf.PI * 2f) * weightShiftAmount;

            spineTarget = originalBodyRot * Quaternion.AngleAxis(twistOffset, spineTwistAxis) * Quaternion.AngleAxis(swayOffset, spineSwayAxis);

            // Hip Bobbing
            float bobOffset = -Mathf.Cos(walkCyclePhase * Mathf.PI * 4f) * bodyBobAmount;
            rootPosTarget = flipRootInitPos + new Vector3(0, bobOffset, 0);

            // Apply calculated rotations
            leftUpTarget = leftUpLegInit * Quaternion.AngleAxis(leftThigh, legSwingAxis);
            rightUpTarget = rightUpLegInit * Quaternion.AngleAxis(rightThigh, legSwingAxis);
            if (leftLowerLeg != null) leftLowerTarget = leftLowerLegInit * Quaternion.AngleAxis(leftKnee, legSwingAxis);
            if (rightLowerLeg != null) rightLowerTarget = rightLowerLegInit * Quaternion.AngleAxis(rightKnee, legSwingAxis);
            if (leftArm != null) leftArmTarget = leftArmInit * Quaternion.AngleAxis(leftArmRot, armSwingAxis);
            if (rightArm != null) rightArmTarget = rightArmInit * Quaternion.AngleAxis(rightArmRot, armSwingAxis);
        }

        // 3. IDLE BREATHING BLEND (Fades in when stopped)
        if (smoothedHorizontalSpeed <= 0.1f && Mathf.Abs(verticalSpeed) <= 1.5f && !isDoingDoubleJumpFlip
            && !isWallSliding)
        {
            float breathCycle = Mathf.Sin(Time.time * breatheSpeed);

            // Pulse scale
            float scaleOffset = breathCycle * breatheAmount;
            frogBody.localScale = originalBodyScale + new Vector3(scaleOffset, scaleOffset, scaleOffset);

            // Subtle chest pitch backward as lungs inflate
            spineTarget = originalBodyRot * Quaternion.AngleAxis(breathCycle * breatheChestTilt, legSwingAxis);
        }
        else
        {
            // Reset scale gracefully when moving
            frogBody.localScale = Vector3.Lerp(frogBody.localScale, originalBodyScale, Time.deltaTime * limbBlendSpeed);
        }

        // 4. APPLY ALL TRANSFORMS SMOOTHLY
        if (leftUpLeg != null) leftUpLeg.localRotation = Quaternion.Slerp(leftUpLeg.localRotation, leftUpTarget, Time.deltaTime * limbBlendSpeed);
        if (rightUpLeg != null) rightUpLeg.localRotation = Quaternion.Slerp(rightUpLeg.localRotation, rightUpTarget, Time.deltaTime * limbBlendSpeed);
        if (leftLowerLeg != null) leftLowerLeg.localRotation = Quaternion.Slerp(leftLowerLeg.localRotation, leftLowerTarget, Time.deltaTime * limbBlendSpeed);
        if (rightLowerLeg != null) rightLowerLeg.localRotation = Quaternion.Slerp(rightLowerLeg.localRotation, rightLowerTarget, Time.deltaTime * limbBlendSpeed);
        if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftArmTarget, Time.deltaTime * limbBlendSpeed);
        if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightArmTarget, Time.deltaTime * limbBlendSpeed);

        if (frogBody != null && !isDoingDoubleJumpFlip)
        {
            frogBody.localRotation = Quaternion.Slerp(frogBody.localRotation, spineTarget, Time.deltaTime * limbBlendSpeed);
        }

        if (flipRootBone != null && !isDoingDoubleJumpFlip)
        {
            flipRootBone.localPosition = Vector3.Lerp(flipRootBone.localPosition, rootPosTarget, Time.deltaTime * limbBlendSpeed);
        }
    }

    public void TriggerProceduralDoubleJump()
    {
        isDoingDoubleJumpFlip = true;
        flipTimer = 0f;
    }

    private void HandleDoubleJumpFlip()
    {
        if (!isDoingDoubleJumpFlip || flipRootBone == null) return;

        flipTimer += Time.deltaTime;
        float flipProgress = flipTimer / flipDuration;

        if (flipProgress >= 1f)
        {
            isDoingDoubleJumpFlip = false;
            flipRootBone.localRotation = flipRootInitRot;
        }
        else
        {
            float flipAngle = Mathf.Lerp(0, -360f, flipProgress);
            flipRootBone.localRotation = flipRootInitRot * Quaternion.Euler(flipAngle, 0, 0);
        }
    }

    public void SetWallSliding(bool sliding, Vector3 wallNormal)
    {
        isWallSliding = sliding;
        wallSlideNormal = wallNormal;
    }

    public void TriggerProceduralWallJump(Vector3 wallNormal)
    {
        isWallSliding = false;
        isDoingWallJumpPush = true;
        wallJumpPushTimer = 0f;
    }

    private void HandleWallJumpPush()
    {
        if (!isDoingWallJumpPush) return;

        wallJumpPushTimer += Time.deltaTime;
        float pushProgress = wallJumpPushTimer / wallJumpPushDuration;

        if (pushProgress >= 1f)
        {
            isDoingWallJumpPush = false;
            return;
        }

        // Kick off from the tucked brace pose to a fully extended pose
        float braceToExtend = Mathf.Lerp(1f, 0f, pushProgress);

        if (leftArm != null) leftArm.localRotation = leftArmInit * Quaternion.AngleAxis(wallBraceArmAngle * braceToExtend, armSwingAxis);
        if (rightArm != null) rightArm.localRotation = rightArmInit * Quaternion.AngleAxis(wallBraceArmAngle * braceToExtend, armSwingAxis);

        float legExtend = Mathf.Lerp(apexTuckAngle, maxFallStretch, pushProgress);
        if (leftUpLeg != null) leftUpLeg.localRotation = leftUpLegInit * Quaternion.AngleAxis(legExtend, legSwingAxis);
        if (rightUpLeg != null) rightUpLeg.localRotation = rightUpLegInit * Quaternion.AngleAxis(legExtend, legSwingAxis);
    }

    private void HandleProceduralAim()
    {
        if (headBone == null || mainCamera == null) return;

        // The cursor is locked/hidden for camera-look, so the real "crosshair" is screen
        // center, i.e. wherever the camera itself is currently pointing.
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f, environmentLayer) ? hit.point : ray.GetPoint(50f);

        Vector3 lookDir = targetPoint - headBone.position;
        if (lookDir == Vector3.zero) return;

        // Decompose directly into pitch/yaw via spherical math instead of round-tripping
        // through Quaternion.eulerAngles, which entangles yaw into the extracted pitch.
        Vector3 localDir = transform.InverseTransformDirection(lookDir.normalized);
        float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg;

        pitch = Mathf.Clamp(pitch, -maxLookAngleVertical, maxLookAngleVertical);
        yaw = Mathf.Clamp(yaw, -maxLookAngleHorizontal, maxLookAngleHorizontal);

        Quaternion clampedLocalRotation = Quaternion.Euler(pitch, yaw, 0f);
        Quaternion targetRotation = transform.rotation * clampedLocalRotation;
        targetRotation *= Quaternion.Euler(headOffsetEuler);

        headBone.rotation = Quaternion.Slerp(headBone.rotation, targetRotation, Time.deltaTime * lookSpeed);
    }
}