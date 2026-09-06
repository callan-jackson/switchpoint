namespace SwitchPoint.Calculation.Numerics;

/// <summary>Options controlling <see cref="RootFinder"/>.</summary>
/// <param name="ValueTolerance">Stop when |f(x)| is below this (default half a penny).</param>
/// <param name="ArgumentTolerance">Stop when the bracket is narrower than this.</param>
/// <param name="MaxIterations">Iteration cap before <see cref="RootNotConvergedException"/> is thrown.</param>
/// <param name="MaxBracketExpansions">How many times the initial bracket may be widened when it does not straddle a root.</param>
public sealed record RootOptions(
    decimal ValueTolerance = 0.005m,
    decimal ArgumentTolerance = 1e-10m,
    int MaxIterations = 200,
    int MaxBracketExpansions = 20)
{
    /// <summary>Defaults suitable for money-valued objective functions.</summary>
    public static RootOptions Default { get; } = new();

    /// <summary>Tighter defaults for rate-valued objective functions (e.g. solving for a yield directly).</summary>
    public static RootOptions ForRates { get; } = new(ValueTolerance: 1e-12m, ArgumentTolerance: 1e-14m);
}

/// <summary>The outcome of a root search.</summary>
/// <param name="Value">The abscissa at which the objective is (within tolerance) zero.</param>
/// <param name="Residual">f(Value) at termination.</param>
/// <param name="Iterations">Brent iterations used, excluding bracket expansion.</param>
/// <param name="BracketExpansions">How many times the bracket was widened before the search began.</param>
public sealed record RootResult(decimal Value, decimal Residual, int Iterations, int BracketExpansions);

/// <summary>Thrown when no sign change can be found even after expanding the bracket.</summary>
public sealed class RootNotBracketedException(string message) : InvalidOperationException(message);

/// <summary>Thrown when the iteration cap is reached before the tolerances are met.</summary>
public sealed class RootNotConvergedException(string message) : InvalidOperationException(message);

/// <summary>
/// Brent's method (inverse quadratic interpolation with secant and bisection fall-backs) on
/// <see cref="decimal"/>. Callers never receive an unconverged value: failure throws.
/// See docs/methodology/numerics.md.
/// </summary>
public static class RootFinder
{
    /// <summary>
    /// Finds x in an (expandable) bracket [<paramref name="lower"/>, <paramref name="upper"/>] with f(x) = 0.
    /// </summary>
    /// <exception cref="RootNotBracketedException">f has the same sign at both ends after all expansions.</exception>
    /// <exception cref="RootNotConvergedException">the iteration cap was reached.</exception>
    public static RootResult Solve(Func<decimal, decimal> f, decimal lower, decimal upper, RootOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        options ??= RootOptions.Default;
        if (upper <= lower)
        {
            throw new ArgumentException("The upper bound must exceed the lower bound.", nameof(upper));
        }

        decimal a = lower;
        decimal b = upper;
        decimal fa = f(a);
        decimal fb = f(b);

        if (fa == 0m)
        {
            return new RootResult(a, 0m, 0, 0);
        }

        if (fb == 0m)
        {
            return new RootResult(b, 0m, 0, 0);
        }

        int expansions = 0;
        while (Math.Sign(fa) == Math.Sign(fb))
        {
            if (expansions >= options.MaxBracketExpansions)
            {
                throw new RootNotBracketedException(
                    $"No sign change in [{a}, {b}] after {expansions} expansions (f(a)={fa}, f(b)={fb}).");
            }

            decimal width = b - a;
            // Widen towards the end whose value is closer to zero; if equal, widen both ways.
            if (Math.Abs(fa) < Math.Abs(fb))
            {
                a -= width;
                fa = f(a);
            }
            else if (Math.Abs(fb) < Math.Abs(fa))
            {
                b += width;
                fb = f(b);
            }
            else
            {
                a -= width;
                b += width;
                fa = f(a);
                fb = f(b);
            }

            expansions++;
            if (fa == 0m)
            {
                return new RootResult(a, 0m, 0, expansions);
            }

            if (fb == 0m)
            {
                return new RootResult(b, 0m, 0, expansions);
            }
        }

        // Brent (Numerical Recipes zbrent, decimal edition).
        decimal c = b;
        decimal fc = fb;
        decimal d = b - a;
        decimal e = d;

        for (int iteration = 1; iteration <= options.MaxIterations; iteration++)
        {
            if (Math.Sign(fb) == Math.Sign(fc))
            {
                c = a;
                fc = fa;
                d = b - a;
                e = d;
            }

            if (Math.Abs(fc) < Math.Abs(fb))
            {
                a = b;
                b = c;
                c = a;
                fa = fb;
                fb = fc;
                fc = fa;
            }

            decimal tol = (2m * 1e-28m * Math.Abs(b)) + (options.ArgumentTolerance / 2m);
            decimal xm = (c - b) / 2m;

            if (Math.Abs(fb) < options.ValueTolerance || Math.Abs(xm) <= tol)
            {
                return new RootResult(b, fb, iteration, expansions);
            }

            if (Math.Abs(e) >= tol && Math.Abs(fa) > Math.Abs(fb))
            {
                decimal s = fb / fa;
                decimal p;
                decimal q;
                if (a == c)
                {
                    // Secant step.
                    p = 2m * xm * s;
                    q = 1m - s;
                }
                else
                {
                    // Inverse quadratic interpolation.
                    decimal qq = fa / fc;
                    decimal r = fb / fc;
                    p = s * ((2m * xm * qq * (qq - r)) - ((b - a) * (r - 1m)));
                    q = (qq - 1m) * (r - 1m) * (s - 1m);
                }

                if (p > 0m)
                {
                    q = -q;
                }

                p = Math.Abs(p);
                decimal min1 = (3m * xm * q) - Math.Abs(tol * q);
                decimal min2 = Math.Abs(e * q);
                if (2m * p < Math.Min(min1, min2))
                {
                    e = d;
                    d = p / q;
                }
                else
                {
                    d = xm;
                    e = d;
                }
            }
            else
            {
                d = xm;
                e = d;
            }

            a = b;
            fa = fb;
            if (Math.Abs(d) > tol)
            {
                b += d;
            }
            else
            {
                b += xm > 0m ? tol : -tol;
            }

            fb = f(b);
            if (fb == 0m)
            {
                return new RootResult(b, 0m, iteration, expansions);
            }
        }

        throw new RootNotConvergedException(
            $"Root search did not converge within {options.MaxIterations} iterations (last x={b}, f={fb}).");
    }
}
