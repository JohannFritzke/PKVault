import { Group } from '@mantine/core';
import { PencilIcon } from 'lucide-react';
import React from 'react';
import { usePkmIndex } from '../../data/hooks/use-pkm-index';
import { useTranslate } from '../../translate/i18n';
import { UIButton } from '../../ui/form/button/ui-button';
import { useDragging } from '../../ui/interaction/move/hooks/use-dragging';
import { useStorageMode } from '../../ui/inventory/context/storage-mode-context';
import { pick } from '../../util/pick';
import { useSelectCallback } from '../../util/use-select-callback';
import type { MoveContainerValue } from '../move/move-container-fns';
import { EvolvePkmButton } from './actions/evolve-pkm-button';
import { MoveButton } from './actions/move-button';
import { OfferTradeButton } from './actions/offer-trade-button';
import { ReleasePkmButton } from './actions/release-pkm-button';

type DetailsDialogActionsProps = {
    id: string;
    saveId: number | null;
    onEdit: () => void;
};

/**
 * Footer of the pokémon dialog: release on the left (care), then move, evolve, trade and edit.
 * Same buttons as the side panel; edit opens the form inside the dialog.
 */
export const DetailsDialogActions: React.FC<DetailsDialogActionsProps> = ({ id, saveId, onEdit }) => {
    const { t } = useTranslate();
    const storageMode = useStorageMode().state;

    const pkmIndexQuery = usePkmIndex(saveId,
        useSelectCallback(data => {
            const pkm = data.data.byId[ id ];
            return pkm && pick(pkm, [ 'id', 'boxId', 'canEdit' ]);
        }, [ id ]));
    const pkm = pkmIndexQuery.data;
    const boxId = pkm?.boxId;

    const container = React.useMemo((): MoveContainerValue => saveId
        ? { type: 'save-item', saveId, boxId: boxId?.toString() ?? '' }
        : { type: 'main-item', boxId: boxId?.toString() ?? '' }, [ boxId, saveId ]);

    const dragging = useDragging(pkm?.id ?? '', container, false);

    return <Group w='100%' gap='sm'>
        {storageMode === 'default' && <ReleasePkmButton
            pkmIds={[ id ]}
            saveId={saveId}
            container={container}
            deleteAllRelatedVariants={false}
        />}

        <div style={{ flexGrow: 1 }} />

        <MoveButton
            // eslint-disable-next-line react-hooks/refs
            ref={dragging.ref}
            // eslint-disable-next-line react-hooks/refs
            useDrag={dragging.useDrag}
            focusOnMount
        />

        {storageMode === 'default' && <>
            <EvolvePkmButton pkmIds={[ id ]} saveId={saveId} />

            <OfferTradeButton pkmIds={[ id ]} saveId={saveId} />

            <UIButton
                name='edit'
                controlLabel={t('storage.actions.edit')}
                variant='filled'
                color='blue'
                size='compact-md'
                leftSection={<PencilIcon />}
                disabled={!pkm?.canEdit}
                onClick={onEdit}
            >
                {t('storage.actions.edit')}
            </UIButton>
        </>}
    </Group>;
};
