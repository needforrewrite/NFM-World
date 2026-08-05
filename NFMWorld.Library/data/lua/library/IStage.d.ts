declare interface IStage
{
    readonly pieces: System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_ITransform_;
    readonly nodes: System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_IAiNode_;
    readonly checkpoints: System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_IAiNode_;
    readonly fixHoops: System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_IAiNode_;
    readonly nlaps: number;
    createObject(objectName: string, x: number, y: number, z: number, xz: number): ITransform;
}
