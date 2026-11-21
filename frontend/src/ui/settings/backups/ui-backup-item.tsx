import { ActionIcon, Button, Group, Table, Text, Tooltip } from '@mantine/core';
import { ArchiveRestoreIcon, TrashIcon } from 'lucide-react';
import React from 'react';
import { useTranslate } from '../../../translate/i18n';
import { renderTime } from '../../../util/render-date-time';
import { WithControlsIcons } from '../../interaction/controls/icons/with-controls-icons';
import { getSelectControl } from '../../interaction/focus-controls/common-controls/select-controls';
import { useFocusControls } from '../../interaction/focus-controls/use-focus-controls';
import { Focus } from '../../interaction/focus/provider/use-focus-context';
import { FocusScope } from '../../interaction/focus/scope/focus-scope';
import { UIConfirmPopover } from '../../popover/ui-confirm-popover';

type UIBackupItemProps = {
    order: number;
    createdAt: string;
    path: string;
    // backup_before_save_bkp_2026-04-25T125948-832Z.zip
    filename: string;
    onRestore: () => void;
    onDelete: () => void;
    disabled?: boolean;
    children: React.ReactNode;
};

export const UIBackupItem: React.FC<UIBackupItemProps> = ({ order, createdAt, path, filename, onRestore, onDelete, disabled, children }) => {
    const { t } = useTranslate();

    const splitBy_ = filename.split('_');
    splitBy_.pop();

    const date = new Date(createdAt);

    const { pushScope } = Focus.usePushPopScope();

    const inputScopeId = createdAt + '-input-scope';

    const { focusProps, controlProps, controlIcons, nodeId, focused } = useFocusControls({
        scopeNodeId: `bkp-${order}`,
        order,
        controls: [
            !disabled && getSelectControl({
                label: t('settings.backups.name.edit'),
                action: () => {
                    if (focused)
                        pushScope(inputScopeId);
                },
            }),
            !disabled && {
                name: 'restore' as const,
                label: t('settings.backups.restore'),
                triggers: {
                    mouse: {
                        type: 'mouse',
                        values: [ 'left-click' ],
                    },
                    gamepad: {
                        type: 'gamepad',
                        values: [ 'X' ],
                    }
                },
                spread: false,
            },
            !disabled && {
                name: 'delete' as const,
                label: t('action.delete'),
                triggers: {
                    mouse: {
                        type: 'mouse',
                        values: [ 'left-click' ],
                    },
                    gamepad: {
                        type: 'gamepad',
                        values: [ 'Y' ],
                    }
                },
                spread: false,
            },
        ],
    });

    return <Table.Tr
        {...focusProps}
        {...controlProps('open')}
        bdrs='md'
    >
        <Table.Td w={1} style={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>
            {renderTime(date)}
        </Table.Td>

        <Table.Td>
            <WithControlsIcons placement='out' icons={controlIcons('open')}>
                <FocusScope id={inputScopeId} parentNodeId={nodeId}>
                    {children}
                </FocusScope>
            </WithControlsIcons>
        </Table.Td>

        <Table.Td maw={0} w='40%'>
            <Tooltip label={path}>
                <Text span c='dimmed' fz='xs' ff='monospace' truncate='end' display='block'>
                    {filename}
                </Text>
            </Tooltip>
        </Table.Td>

        <Table.Td w={1}>
            <Group gap='xs' wrap='nowrap' justify='flex-end'>
                <UIConfirmPopover label={t('settings.backups.restore')} color='red' action={onRestore}>
                    <WithControlsIcons placement='out' icons={controlIcons('restore')}>
                        <Button
                            variant='light'
                            color='red'
                            size='compact-sm'
                            leftSection={<ArchiveRestoreIcon size={14} />}
                            {...controlProps('restore')}
                        >
                            {t('settings.backups.restore')}
                        </Button>
                    </WithControlsIcons>
                </UIConfirmPopover>

                <UIConfirmPopover label={t('action.delete')} color='red' action={onDelete}>
                    <WithControlsIcons placement='out' icons={controlIcons('delete')}>
                        <ActionIcon variant='subtle' color='red' aria-label={t('action.delete')} {...controlProps('delete')}>
                            <TrashIcon size={16} />
                        </ActionIcon>
                    </WithControlsIcons>
                </UIConfirmPopover>
            </Group>
        </Table.Td>
    </Table.Tr>;
};
