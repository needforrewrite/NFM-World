declare interface ITransform
{
    readonly childTransforms: System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_ITransform_;
    position: fixed64vector3;
    rotation: fixed64euler;
    readonly parent: ITransform;
}
