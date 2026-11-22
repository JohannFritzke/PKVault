import { EntityContext } from '../../../../data/sdk/model';

/**
 * Generation of a game context (Let's Go = 7, Legends Arceus / BDSP = 8, Legends Z-A = 9).
 * Same mapping as SingleLocation.ContextGeneration in the Core.
 */
export const contextGeneration = (context: EntityContext): number => {
    switch (context) {
        case EntityContext.Gen7b: return 7;
        case EntityContext.Gen8a:
        case EntityContext.Gen8b: return 8;
        case EntityContext.Gen9a: return 9;
        default: return context;
    }
};
