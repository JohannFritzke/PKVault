import React from "react";
import { usePkmIndex } from '../../data/hooks/use-pkm-index';
import { withErrorCatcher } from '../../error/with-error-catcher';
import { Route } from '../../routes/storage';
import { useMoveContext } from '../../ui/interaction/move/context/use-move-context';
import { UIStorageDetailsDialog } from '../../ui/storage/storage-details/dialog/ui-storage-details-dialog';
import { useCurrentStorage } from '../panel/storage-panel-context';
import { StorageDetailsDialogInner } from './storage-details-dialog-inner';

/**
 * Details of the selected pokémon (storage or save) in a dialog.
 * Hidden while the pokémon is moved; closed once it is dropped.
 */
export const StorageDetails: React.FC = withErrorCatcher('default', () => {
  const navigate = Route.useNavigate();

  const { getSelected } = useCurrentStorage();
  const selectedSaveId = Route.useSearch({ select: search => getSelected(search.selected)?.saveId });
  const selectedId = Route.useSearch({ select: search => getSelected(search.selected)?.id }) ?? '';

  const pkmIndexQuery = usePkmIndex(selectedSaveId ?? null, data => !!data.data.byId[ selectedId ]);
  const noPkmsFound = !!selectedId && pkmIndexQuery.data === false;

  const { useMoveStore } = useMoveContext();
  const isMoveDragging = useMoveStore(({ state }) => state.status === 'dragging');

  const unselect = React.useCallback(() => navigate({
    search: {
      selected: undefined,
    },
  }), [ navigate ]);

  // unselect if pkm does not exist
  React.useEffect(() => {
    if (noPkmsFound) {
      unselect();
    }
  }, [ unselect, noPkmsFound ]);

  // "Move" closes the dialog: the pokémon stays in hand, and the dialog does not come back after the drop
  const [ wasDragging, setWasDragging ] = React.useState(false);
  if (isMoveDragging && !wasDragging) {
    setWasDragging(true);
  }
  React.useEffect(() => {
    if (!isMoveDragging && wasDragging) {
      setWasDragging(false);
      void unselect();
    }
  }, [ isMoveDragging, wasDragging, unselect ]);

  const opened = !!selectedId && !isMoveDragging && !wasDragging;

  return <UIStorageDetailsDialog opened={opened} onClose={unselect}>
    {opened && <StorageDetailsDialogInner
      key={`${selectedSaveId}-${selectedId}`}
      id={selectedId}
      saveId={selectedSaveId ?? null}
      onClose={unselect}
    />}
  </UIStorageDetailsDialog>;
});
