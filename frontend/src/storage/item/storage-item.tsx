import React from "react";
import type { MoveContainerValue } from '../move/move-container-fns';
import { useCurrentStorage } from '../panel/storage-panel-context';
import { UIStorageItem, type UIStorageItemProps } from '../../ui/storage/storage-item/ui-storage-item';
import { SpeciesImg, type SpeciesImgProps } from '../../img/species-img';
import classes from '../../ui/storage/storage-item/base/ui-storage-item-base.module.css';

export type StorageItemProps =
  & Pick<UIStorageItemProps<MoveContainerValue>, 'id' | 'nodeId' | 'selected' | 'container' | 'name' | 'level' | 'slot' | 'onClick' | 'selectFromPreviousSelected' | 'icons' | 'heldItem'>
  & Pick<SpeciesImgProps, 'species' | 'context' | 'form' | 'isFemale' | 'isShiny' | 'isEgg' | 'isShadow'>
  & {
    /** Cannot be traded (duplicate or illegal): dashed border, dimmed sprite. */
    tradeBlocked?: boolean;
  };

export const StorageItem: React.FC<StorageItemProps> = React.memo(({
  species,
  context,
  form,
  isFemale,
  isEgg,
  isShiny,
  isShadow,
  tradeBlocked,

  ...rest
}) => {
  const { storageIndex } = useCurrentStorage();

  return (
    <UIStorageItem
      globalOrder={storageIndex * 1000 + rest.slot}
      data-trade-block={tradeBlocked || undefined}
      {...rest}
    >
      <SpeciesImg className={tradeBlocked ? classes.species : undefined} species={species} context={context} form={form} isFemale={isFemale} isShiny={isShiny} isEgg={isEgg} isShadow={isShadow} />
    </UIStorageItem>
  );
});
