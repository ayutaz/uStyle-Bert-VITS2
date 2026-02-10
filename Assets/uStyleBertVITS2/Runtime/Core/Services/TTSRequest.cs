namespace uStyleBertVITS2.Services
{
    /// <summary>
    /// TTS合成リクエスト。immutableな値オブジェクト。
    /// </summary>
    public readonly struct TTSRequest
    {
        public readonly string Text;
        public readonly int SpeakerId;
        public readonly int StyleId;
        public readonly float SdpRatio;
        public readonly float NoiseScale;
        public readonly float NoiseScaleW;
        public readonly float LengthScale;
        public readonly float StyleWeight;

        public TTSRequest(
            string text,
            int speakerId = 0,
            int styleId = 0,
            float sdpRatio = 0.2f,
            float noiseScale = 0.6f,
            float noiseScaleW = 0.8f,
            float lengthScale = 1.0f,
            float styleWeight = 1.0f)
        {
            Text = text;
            SpeakerId = speakerId;
            StyleId = styleId;
            SdpRatio = sdpRatio;
            NoiseScale = noiseScale;
            NoiseScaleW = noiseScaleW;
            LengthScale = lengthScale;
            StyleWeight = styleWeight;
        }
    }
}
