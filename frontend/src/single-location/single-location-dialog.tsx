import { Alert, Group, List, Modal, Stack, Text } from '@mantine/core';
import { useQueryClient } from '@tanstack/react-query';
import { CheckIcon, MapPinIcon, TriangleAlertIcon } from 'lucide-react';
import React from 'react';
import { SingleLocationError, type SingleLocationResultDTO } from '../data/sdk/model';
import { useSingleLocationResolve, useSingleLocationStatus } from '../data/sdk/single-location/single-location.gen';
import { useTranslate } from '../translate/i18n';
import { UIButton } from '../ui/form/button/ui-button';
import { useControls } from '../ui/interaction/controls/use-controls';
import { getBackControl } from '../ui/interaction/focus-controls/common-controls/back-controls';
import { FocusScope } from '../ui/interaction/focus/scope/focus-scope';
import { useFocusScopeContext } from '../ui/interaction/focus/scope/use-focus-scope-context';

/**
 * A pokémon is either in a save or in the storage.
 * Shown when pokémon linked to a save (or copied after "detach") remain from before this rule.
 */
export const SingleLocationDialog: React.FC = () => {
    const { t } = useTranslate();

    const status = useSingleLocationStatus().data?.data;
    const [ dismissed, setDismissed ] = React.useState(false);

    const opened = !dismissed && !!status && (status.inSaveCount > 0 || status.linkOnlyCount > 0);
    const onClose = () => setDismissed(true);

    return <Modal
        opened={opened}
        keepMounted={false}
        onClose={onClose}
        size='lg'
        title={<Group gap='sm'>
            <MapPinIcon />
            <Text size='lg'>{t('single-location.title')}</Text>
        </Group>}
    >
        {status && <SingleLocationDialogInner
            inSaveCount={status.inSaveCount}
            linkOnlyCount={status.linkOnlyCount}
            onClose={onClose}
        />}
    </Modal>;
};

const errorKeys = {
    [ SingleLocationError.PENDING_CHANGES ]: 'single-location.error.pending-changes',
    [ SingleLocationError.FAILED ]: 'single-location.error.failed',
} as const;

const SingleLocationDialogInner: React.FC<{ inSaveCount: number; linkOnlyCount: number; onClose: () => void }> = ({ inSaveCount, linkOnlyCount, onClose }) => {
    const { t } = useTranslate();
    const queryClient = useQueryClient();

    const parentScope = useFocusScopeContext();
    const { controlProps } = useControls(
        'single-location-dialog',
        true,
        parentScope.parentsIds.length + 1,
        [ getBackControl({ label: t('action.back'), action: onClose }) ],
        { enabled: true },
    );

    const resolveMutation = useSingleLocationResolve();
    const [ result, setResult ] = React.useState<SingleLocationResultDTO>();

    const resolve = async () => {
        const response = await resolveMutation.mutateAsync();
        setResult(response.data);
        if (response.data.success) {
            await queryClient.invalidateQueries();
            onClose();
        }
    };

    return <FocusScope id='single-location-dialog' focusOnMount restoreMode='none'>
        <Stack {...controlProps('back')}>
            <Text fz='sm'>{t('single-location.description')}</Text>

            <List fz='sm' spacing='xs'>
                {inSaveCount > 0 && <List.Item>{t('single-location.in-save', { count: inSaveCount })}</List.Item>}
                {linkOnlyCount > 0 && <List.Item>{t('single-location.link-only', { count: linkOnlyCount })}</List.Item>}
            </List>

            <Text c='dimmed' fz='sm'>{t('single-location.backup')}</Text>

            {result && !result.success && result.error !== undefined && <Alert variant='outline' color='yellow' icon={<TriangleAlertIcon />}>
                {t(errorKeys[ result.error ])}
            </Alert>}

            <Group justify='flex-end'>
                <UIButton name='single-location-later' controlLabel={t('single-location.later')} variant='default' onClick={onClose}>
                    {t('single-location.later')}
                </UIButton>
                <UIButton
                    name='single-location-resolve'
                    controlLabel={t('single-location.resolve')}
                    variant='filled'
                    leftSection={<CheckIcon />}
                    loading={resolveMutation.isPending}
                    onClick={resolve}
                >
                    {t('single-location.resolve')}
                </UIButton>
            </Group>
        </Stack>
    </FocusScope>;
};
