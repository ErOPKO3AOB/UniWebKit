namespace UniWebKit
{
    public interface IUniWebFeature
    {
        /// <summary>
        /// Показывает, поддерживается ли этот функционал на текущей платформе.
        /// UI должен проверять это поле, чтобы скрывать нерабочие кнопки.
        /// </summary>
        bool IsSupported { get; }
    }
}