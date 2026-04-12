namespace NatureProtector.Prevention.Host.Processing;

public interface IProcessingFailureClassifier
{
    ProcessingFailureClassification Classify(Exception exception);
}
