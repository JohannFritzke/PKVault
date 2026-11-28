import { Alert, Group, Stack } from '@mantine/core';
import { ShieldXIcon } from 'lucide-react';
import React from 'react';
import { usePkmIndex } from '../../data/hooks/use-pkm-index';
import { tradeBlockText } from '../../trades/trade-util';
import { useTradeBlock } from '../../trades/use-trade-blocked';
import { useTranslate } from '../../translate/i18n';
import { UIButton } from '../../ui/form/button/ui-button';
import { useControls } from '../../ui/interaction/controls/use-controls';
import { getBackControl } from '../../ui/interaction/focus-controls/common-controls/back-controls';
import { popoverContext, type PopoverContext } from '../../ui/interaction/focus-controls/components/popover/context/popover-context';
import { FocusScope } from '../../ui/interaction/focus/scope/focus-scope';
import { useFocusScopeContext } from '../../ui/interaction/focus/scope/use-focus-scope-context';
import { UIStorageDetailsDialogLayout } from '../../ui/storage/storage-details/dialog/ui-storage-details-dialog';
import { pick } from '../../util/pick';
import { useSelectCallback } from '../../util/use-select-callback';
import { DetailsContent } from './details-content';
import { DetailsDialogActions } from './details-dialog-actions';
import { DetailsEdit } from './details-edit';
import { DetailsMain } from './details-main';
import { DetailsSaves } from './details-saves';

type StorageDetailsDialogInnerProps = {
  id: string;
  saveId: number | null;
  onClose: () => void;
};

export const StorageDetailsDialogInner: React.FC<StorageDetailsDialogInnerProps> = ({ id, saveId, onClose }) => {
  const { t } = useTranslate();

  const [ editing, setEditing ] = React.useState(false);

  const parentScope = useFocusScopeContext();
  const { controlProps } = useControls(
    'storage-details-dialog',
    true,
    parentScope.parentsIds.length + 1,
    [ getBackControl({
      label: editing ? t('action.cancel') : t('action.close'),
      action: () => editing ? setEditing(false) : onClose(),
    }) ],
    { enabled: true },
  );

  const pkmQuery = usePkmIndex(saveId,
    useSelectCallback(data => {
      const pkm = data.data.byId[ id ];
      return pkm && pick(pkm, [ 'id', 'boxId' ]);
    }, [ id ]));
  const block = useTradeBlock(saveId === null ? pkmQuery.data?.boxId.toString() : undefined, saveId === null ? id : undefined);

  // the edit form closes itself like a popover once submitted
  const editContext = React.useMemo((): PopoverContext => ({
    opened: editing,
    setOpened: value => setEditing(typeof value === 'function' ? value(editing) : value),
  }), [ editing ]);

  return <FocusScope id='storage-details-dialog' focusOnMount restoreMode='none'>
    <Stack gap={0} {...controlProps('back')}>
      <UIStorageDetailsDialogLayout
        onClose={onClose}
        header={actions => <DetailsSaves actions={actions} />}
        main={<DetailsMain />}
        notice={block && <Alert variant='outline' color='yellow' icon={<ShieldXIcon />} title={t('details.trade-block.title')}>
          {tradeBlockText(t, block)}
        </Alert>}
        body={editing
          ? <Stack px='lg' pb='md' style={{ overflowY: 'auto' }}>
            <popoverContext.Provider value={editContext}>
              <DetailsEdit pkmId={id} saveId={saveId} />
            </popoverContext.Provider>
          </Stack>
          : <DetailsContent />}
        footer={editing
          ? <Group justify='flex-end'>
            <UIButton
              name='edit-cancel'
              controlLabel={t('action.cancel')}
              variant='default'
              size='compact-md'
              onClick={() => setEditing(false)}
            >
              {t('action.cancel')}
            </UIButton>
          </Group>
          : <DetailsDialogActions id={id} saveId={saveId} onEdit={() => setEditing(true)} />}
      />
    </Stack>
  </FocusScope>;
};
