namespace SwitchPoint.Calculation.Numerics;

/// <summary>
/// Transcendental functions on <see cref="decimal"/>. Every routine converges to better than 1e-25
/// absolute error inside the documented domain and never touches <see cref="double"/>, so results
/// are identical on every platform and run. See docs/methodology/numerics.md.
/// </summary>
public static class DecimalMath
{
    /// <summary>Euler's number to 28 significant figures.</summary>
    public const decimal E = 2.7182818284590452353602874714m;

    /// <summary>Natural logarithm of 2 to 28 significant figures.</summary>
    public const decimal Ln2 = 0.6931471805599453094172321215m;

    /// <summary>Pi to 28 significant figures.</summary>
    public const decimal Pi = 3.1415926535897932384626433833m;

    private const decimal SeriesEpsilon = 1e-28m;
    private const int MaxSeriesTerms = 500;

    /// <summary>e raised to the power <paramref name="x"/>.</summary>
    /// <remarks>
    /// Argument is halved until |x| ≤ 0.5, the Taylor series is summed to 1e-28, and the result is
    /// squared back. Values below about 1e-28 underflow to zero; values above ~66.5 overflow the
    /// decimal range and throw <see cref="OverflowException"/>.
    /// </remarks>
    public static decimal Exp(decimal x)
    {
        if (x == 0m)
        {
            return 1m;
        }

        if (x < -66m)
        {
            return 0m; // below decimal's smallest representable magnitude
        }

        int halvings = 0;
        decimal y = x;
        while (Math.Abs(y) > 0.5m)
        {
            y /= 2m;
            halvings++;
        }

        decimal term = 1m;
        decimal sum = 1m;
        for (int n = 1; n < MaxSeriesTerms; n++)
        {
            term = term * y / n;
            sum += term;
            if (Math.Abs(term) < SeriesEpsilon)
            {
                break;
            }
        }

        for (int i = 0; i < halvings; i++)
        {
            sum *= sum;
        }

        return sum;
    }

    /// <summary>Natural logarithm of <paramref name="x"/> (x &gt; 0).</summary>
    /// <remarks>
    /// x is scaled by powers of two into [0.75, 1.5] and ln is evaluated with the inverse hyperbolic
    /// tangent series 2·Σ z^(2n+1)/(2n+1), z = (m−1)/(m+1), |z| ≤ 0.2.
    /// </remarks>
    public static decimal Ln(decimal x)
    {
        if (x <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "The natural logarithm is defined for positive values only.");
        }

        if (x == 1m)
        {
            return 0m;
        }

        int k = 0;
        decimal m = x;
        while (m > 1.5m)
        {
            m /= 2m;
            k++;
        }

        while (m < 0.75m)
        {
            m *= 2m;
            k--;
        }

        decimal z = (m - 1m) / (m + 1m);
        decimal z2 = z * z;
        decimal term = z;
        decimal sum = 0m;
        for (int n = 1; n < MaxSeriesTerms; n += 2)
        {
            decimal contribution = term / n;
            sum += contribution;
            if (Math.Abs(contribution) < SeriesEpsilon)
            {
                break;
            }

            term *= z2;
        }

        return (2m * sum) + (k * Ln2);
    }

    /// <summary><paramref name="x"/> raised to the power <paramref name="y"/>.</summary>
    /// <remarks>
    /// Integer exponents (|y| ≤ 4096) use exact binary exponentiation so that (1.05)^12 is computed
    /// without series error. Non-integer exponents require x &gt; 0 and use exp(y·ln x).
    /// </remarks>
    public static decimal Pow(decimal x, decimal y)
    {
        if (y == 0m)
        {
            return 1m;
        }

        bool integerExponent = y == decimal.Truncate(y) && Math.Abs(y) <= 4096m;

        if (x == 0m)
        {
            if (y > 0m)
            {
                return 0m;
            }

            throw new ArgumentOutOfRangeException(nameof(y), y, "Zero cannot be raised to a negative power.");
        }

        if (integerExponent)
        {
            return IntegerPow(x, (int)y);
        }

        if (x < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "A negative base requires an integer exponent.");
        }

        return Exp(y * Ln(x));
    }

    /// <summary>Exact power for integer exponents by repeated squaring.</summary>
    public static decimal IntegerPow(decimal x, int n)
    {
        if (n == 0)
        {
            return 1m;
        }

        bool negative = n < 0;
        long e = Math.Abs((long)n);
        decimal result = 1m;
        decimal b = x;
        while (e > 0)
        {
            if ((e & 1) == 1)
            {
                result *= b;
            }

            e >>= 1;
            if (e > 0)
            {
                b *= b;
            }
        }

        return negative ? 1m / result : result;
    }

    /// <summary>Square root by Newton's method, exact to the last decimal digit for perfect squares.</summary>
    public static decimal Sqrt(decimal x)
    {
        if (x < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Cannot take the square root of a negative number.");
        }

        if (x == 0m)
        {
            return 0m;
        }

        // Start from a power-of-two estimate so Newton converges in a handful of steps at any scale.
        decimal guess = 1m;
        decimal scaled = x;
        while (scaled >= 4m)
        {
            scaled /= 4m;
            guess *= 2m;
        }

        while (scaled < 0.25m)
        {
            scaled *= 4m;
            guess /= 2m;
        }

        decimal previous;
        int iterations = 0;
        do
        {
            previous = guess;
            guess = (guess + (x / guess)) / 2m;
            iterations++;
        }
        while (Math.Abs(guess - previous) > SeriesEpsilon && iterations < 200);

        return guess;
    }

    /// <summary>The <paramref name="n"/>th root of <paramref name="x"/> (x ≥ 0, n ≥ 1), polished with one Newton step.</summary>
    public static decimal NthRoot(decimal x, int n)
    {
        if (n < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "The root index must be at least 1.");
        }

        if (x < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Cannot take a root of a negative number.");
        }

        if (x == 0m || n == 1)
        {
            return x;
        }

        if (n == 2)
        {
            return Sqrt(x);
        }

        decimal r = Exp(Ln(x) / n);

        // One Newton polish: r ← r − (rⁿ − x) / (n·rⁿ⁻¹)
        decimal rPow = IntegerPow(r, n - 1);
        r -= ((rPow * r) - x) / (n * rPow);
        return r;
    }
}
