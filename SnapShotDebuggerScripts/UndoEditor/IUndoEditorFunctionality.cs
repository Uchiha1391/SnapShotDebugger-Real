public interface IUndoEditorFunctionality
{
    void RefreshUndoMethodFilteredList();
    void UndoTillThis(int index);
    void UndoThis(int index);
    void RedoTillThis(int index);
    void RedoThis(int index);
}