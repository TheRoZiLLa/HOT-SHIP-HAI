// ============================================================================
// AudioSpectrogram.cs
// Utility class for computing mel spectrograms from raw audio data.
// Used by TeachableMachineProvider for AI model preprocessing.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;

namespace HotShipHai.Voice
{
    /// <summary>
    /// Computes mel spectrograms from raw PCM audio data.
    /// Matches the preprocessing used by Teachable Machine sound models.
    /// <para>
    /// Standard Teachable Machine input: [1, 43, 232, 1] mel spectrogram
    /// = 43 time frames × 232 mel frequency bins from 1 second of 44.1kHz audio.
    /// </para>
    /// </summary>
    public class AudioSpectrogram
    {
        private readonly int _fftSize;
        private readonly int _hopLength;
        private readonly int _melBins;
        private readonly int _timeFrames;
        private readonly int _sampleRate;

        // Precomputed mel filterbank
        private float[,] _melFilterbank;

        // FFT working buffers
        private float[] _fftReal;
        private float[] _fftImag;
        private float[] _window;
        private float[] _magnitudes;

        public AudioSpectrogram(int fftSize, int hopLength, int melBins, int timeFrames, int sampleRate)
        {
            _fftSize = fftSize;
            _hopLength = hopLength;
            _melBins = melBins;
            _timeFrames = timeFrames;
            _sampleRate = sampleRate;

            // Allocate buffers
            _fftReal = new float[fftSize];
            _fftImag = new float[fftSize];
            _magnitudes = new float[fftSize / 2 + 1];

            // Precompute Hann window
            _window = new float[fftSize];
            for (int i = 0; i < fftSize; i++)
            {
                _window[i] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (fftSize - 1)));
            }

            // Precompute mel filterbank
            BuildMelFilterbank();
        }

        /// <summary>
        /// Compute a mel spectrogram from raw audio samples.
        /// </summary>
        /// <param name="samples">Raw PCM audio samples (1 second at sampleRate).</param>
        /// <returns>Mel spectrogram as [timeFrames, melBins] flattened array.</returns>
        public float[] Compute(float[] samples)
        {
            float[] spectrogram = new float[_timeFrames * _melBins];

            for (int t = 0; t < _timeFrames; t++)
            {
                int offset = t * _hopLength;

                // Apply window and prepare FFT input
                for (int i = 0; i < _fftSize; i++)
                {
                    int sampleIdx = offset + i;
                    _fftReal[i] = (sampleIdx < samples.Length)
                        ? samples[sampleIdx] * _window[i]
                        : 0f;
                    _fftImag[i] = 0f;
                }

                // Compute FFT
                FFT(_fftReal, _fftImag, _fftSize);

                // Compute magnitude spectrum
                int numBins = _fftSize / 2 + 1;
                for (int i = 0; i < numBins; i++)
                {
                    _magnitudes[i] = Mathf.Sqrt(
                        _fftReal[i] * _fftReal[i] + _fftImag[i] * _fftImag[i]
                    );
                }

                // Apply mel filterbank
                for (int m = 0; m < _melBins; m++)
                {
                    float melVal = 0f;
                    for (int k = 0; k < numBins; k++)
                    {
                        melVal += _magnitudes[k] * _melFilterbank[m, k];
                    }

                    // Log mel (add small epsilon to avoid log(0))
                    spectrogram[t * _melBins + m] = Mathf.Log(melVal + 1e-6f);
                }
            }

            return spectrogram;
        }

        // ====================================================================
        // FFT (Cooley-Tukey radix-2)
        // ====================================================================

        private static void FFT(float[] real, float[] imag, int n)
        {
            // Bit-reversal permutation
            int j = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < j)
                {
                    (real[i], real[j]) = (real[j], real[i]);
                    (imag[i], imag[j]) = (imag[j], imag[i]);
                }

                int m = n >> 1;
                while (m >= 1 && j >= m)
                {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }

            // Butterfly operations
            for (int len = 2; len <= n; len <<= 1)
            {
                float angle = -2f * Mathf.PI / len;
                float wReal = Mathf.Cos(angle);
                float wImag = Mathf.Sin(angle);

                for (int i = 0; i < n; i += len)
                {
                    float curReal = 1f, curImag = 0f;

                    for (int k = 0; k < len / 2; k++)
                    {
                        int even = i + k;
                        int odd = even + len / 2;

                        float tReal = curReal * real[odd] - curImag * imag[odd];
                        float tImag = curReal * imag[odd] + curImag * real[odd];

                        real[odd] = real[even] - tReal;
                        imag[odd] = imag[even] - tImag;
                        real[even] += tReal;
                        imag[even] += tImag;

                        float newCurReal = curReal * wReal - curImag * wImag;
                        curImag = curReal * wImag + curImag * wReal;
                        curReal = newCurReal;
                    }
                }
            }
        }

        // ====================================================================
        // Mel Filterbank
        // ====================================================================

        private void BuildMelFilterbank()
        {
            int numBins = _fftSize / 2 + 1;
            _melFilterbank = new float[_melBins, numBins];

            float melMin = HzToMel(0f);
            float melMax = HzToMel(_sampleRate / 2f);

            // Create mel-spaced center frequencies
            float[] melPoints = new float[_melBins + 2];
            for (int i = 0; i < melPoints.Length; i++)
            {
                melPoints[i] = melMin + (melMax - melMin) * i / (melPoints.Length - 1);
            }

            // Convert back to Hz and then to FFT bin indices
            float[] binIndices = new float[melPoints.Length];
            for (int i = 0; i < melPoints.Length; i++)
            {
                float hz = MelToHz(melPoints[i]);
                binIndices[i] = hz * (_fftSize + 1) / _sampleRate;
            }

            // Build triangular filters
            for (int m = 0; m < _melBins; m++)
            {
                float left = binIndices[m];
                float center = binIndices[m + 1];
                float right = binIndices[m + 2];

                for (int k = 0; k < numBins; k++)
                {
                    if (k >= left && k <= center && center > left)
                    {
                        _melFilterbank[m, k] = (k - left) / (center - left);
                    }
                    else if (k > center && k <= right && right > center)
                    {
                        _melFilterbank[m, k] = (right - k) / (right - center);
                    }
                    else
                    {
                        _melFilterbank[m, k] = 0f;
                    }
                }
            }
        }

        private static float HzToMel(float hz)
        {
            return 2595f * Mathf.Log10(1f + hz / 700f);
        }

        private static float MelToHz(float mel)
        {
            return 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);
        }
    }
}
