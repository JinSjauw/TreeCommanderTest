using UnityEngine;

/// <summary>
/// Cached coefficients for damped spring motion.
/// Compute once via SpringMath, reuse across many springs sharing the same dt/ω/ζ.
/// </summary>
public struct DampedSpringMotionParams
{
    // newPos = posPosCoef * oldPos + posVelCoef * oldVel
    public float posPosCoef, posVelCoef;
    // newVel = velPosCoef * oldPos + velVelCoef * oldVel
    public float velPosCoef, velVelCoef;
}

/// <summary>
/// Reusable damped spring state for a Vector3 value.
/// Tracks position, velocity, and equilibrium (target) in one struct.
/// </summary>
[System.Serializable]
public struct SpringState
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 equilibrium;

    public SpringState(Vector3 startPosition)
    {
        position = startPosition;
        velocity = Vector3.zero;
        equilibrium = startPosition;
    }

    /// <summary>Advance the spring by one timestep.</summary>
    public void Update(float deltaTime, float angularFrequency, float dampingRatio)
    {
        var p = SpringMath.CalcDampedSpringMotionParams(deltaTime, angularFrequency, dampingRatio);

        SpringMath.UpdateDampedSpring(ref position.x, ref velocity.x, equilibrium.x, p);
        SpringMath.UpdateDampedSpring(ref position.y, ref velocity.y, equilibrium.y, p);
        SpringMath.UpdateDampedSpring(ref position.z, ref velocity.z, equilibrium.z, p);
    }

    /// <summary>Add an instantaneous velocity impulse (e.g. recoil kick).</summary>
    public void AddImpulse(Vector3 velocityDelta)
    {
        velocity += velocityDelta;
    }

    /// <summary>Instantly snap position to equilibrium with zero velocity.</summary>
    public void Snap()
    {
        position = equilibrium;
        velocity = Vector3.zero;
    }

    /// <summary>Instantly snap to a given target and set it as equilibrium.</summary>
    public void SnapTo(Vector3 target)
    {
        equilibrium = target;
        position = target;
        velocity = Vector3.zero;
    }
}

/// <summary>
/// Damped spring math based on Ryan Juckett's implementation.
/// https://www.ryanjuckett.com/damped-springs/
///
/// Copyright (c) 2008-2012 Ryan Juckett
/// This software is provided 'as-is', without any express or implied
/// warranty. In no event will the authors be held liable for any damages
/// arising from the use of this software.
/// Permission is granted to anyone to use this software for any purpose,
/// including commercial applications, and to alter it and redistribute it
/// freely, subject to the following restrictions:
/// 1. The origin of this software must not be misrepresented; you must not
///    claim that you wrote the original software.
/// 2. Altered source versions must be plainly marked as such, and must not be
///    misrepresented as being the original software.
/// 3. This notice may not be removed or altered from any source distribution.
/// </summary>
public static class SpringMath
{
    private const float Epsilon = 0.0001f;

    /// <summary>
    /// Compute cached motion coefficients for a given time step and spring parameters.
    /// Call once per frame (or once per shared-param group), then call UpdateDampedSpring
    /// for each spring using the returned params.
    ///
    /// dampingRatio &lt; 1: under-damped (bounces)
    /// dampingRatio = 1: critically damped (fastest settling with no bounce)
    /// dampingRatio &gt; 1: over-damped (smooth, no bounce, slower)
    /// </summary>
    public static DampedSpringMotionParams CalcDampedSpringMotionParams(
        float deltaTime,
        float angularFrequency,
        float dampingRatio)
    {
        DampedSpringMotionParams p = default;

        if (dampingRatio < 0f) dampingRatio = 0f;
        if (angularFrequency < 0f) angularFrequency = 0f;

        // No angular frequency → no motion (identity)
        if (angularFrequency < Epsilon)
        {
            p.posPosCoef = 1f; p.posVelCoef = 0f;
            p.velPosCoef = 0f; p.velVelCoef = 1f;
            return p;
        }

        if (dampingRatio > 1f + Epsilon)
        {
            // Over-damped
            float za = -angularFrequency * dampingRatio;
            float zb = angularFrequency * Mathf.Sqrt(dampingRatio * dampingRatio - 1f);
            float z1 = za - zb;
            float z2 = za + zb;

            float e1 = Mathf.Exp(z1 * deltaTime);
            float e2 = Mathf.Exp(z2 * deltaTime);
            float invTwoZb = 1f / (2f * zb);
            float e1_Over_TwoZb = e1 * invTwoZb;
            float e2_Over_TwoZb = e2 * invTwoZb;
            float z1e1_Over_TwoZb = z1 * e1_Over_TwoZb;
            float z2e2_Over_TwoZb = z2 * e2_Over_TwoZb;

            p.posPosCoef = e1_Over_TwoZb * z2 - z2e2_Over_TwoZb + e2;
            p.posVelCoef = -e1_Over_TwoZb + e2_Over_TwoZb;
            p.velPosCoef = (z1e1_Over_TwoZb - z2e2_Over_TwoZb + e2) * z2;
            p.velVelCoef = -z1e1_Over_TwoZb + z2e2_Over_TwoZb;
        }
        else if (dampingRatio < 1f - Epsilon)
        {
            // Under-damped
            float omegaZeta = angularFrequency * dampingRatio;
            float alpha = angularFrequency * Mathf.Sqrt(1f - dampingRatio * dampingRatio);
            float expTerm = Mathf.Exp(-omegaZeta * deltaTime);
            float cosTerm = Mathf.Cos(alpha * deltaTime);
            float sinTerm = Mathf.Sin(alpha * deltaTime);
            float invAlpha = 1f / alpha;
            float expSin = expTerm * sinTerm;
            float expCos = expTerm * cosTerm;
            float expOmegaZetaSin_Over_Alpha = expTerm * omegaZeta * sinTerm * invAlpha;

            p.posPosCoef = expCos + expOmegaZetaSin_Over_Alpha;
            p.posVelCoef = expSin * invAlpha;
            p.velPosCoef = -expSin * alpha - omegaZeta * expOmegaZetaSin_Over_Alpha;
            p.velVelCoef = expCos - expOmegaZetaSin_Over_Alpha;
        }
        else
        {
            // Critically damped
            float expTerm = Mathf.Exp(-angularFrequency * deltaTime);
            float timeExp = deltaTime * expTerm;
            float timeExpFreq = timeExp * angularFrequency;

            p.posPosCoef = timeExpFreq + expTerm;
            p.posVelCoef = timeExp;
            p.velPosCoef = -angularFrequency * timeExpFreq;
            p.velVelCoef = -timeExpFreq + expTerm;
        }

        return p;
    }

    /// <summary>
    /// Update a single float position and velocity using pre-computed motion params.
    /// Works in equilibrium-relative space internally.
    /// </summary>
    public static void UpdateDampedSpring(
        ref float position,
        ref float velocity,
        float equilibrium,
        DampedSpringMotionParams p)
    {
        float oldPos = position - equilibrium;
        float oldVel = velocity;
        position = oldPos * p.posPosCoef + oldVel * p.posVelCoef + equilibrium;
        velocity = oldPos * p.velPosCoef + oldVel * p.velVelCoef;
    }
}
