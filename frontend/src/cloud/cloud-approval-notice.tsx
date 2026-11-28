import { Badge, Group, Paper, Stack, Text, ThemeIcon } from '@mantine/core';
import { useQueryClient } from '@tanstack/react-query';
import { BanIcon, ClockIcon, RefreshCwIcon } from 'lucide-react';
import React from 'react';
import { useCloudSync } from '../data/sdk/cloud/cloud.gen';
import { CloudAccountStatus } from '../data/sdk/model';
import { useTranslate } from '../translate/i18n';
import { UIButton } from '../ui/form/button/ui-button';

/**
 * Account not approved by the server owner (or blocked): cloud backups and trades are off,
 * the rest of the app works locally. "Check again" asks the server right away.
 */
export const CloudApprovalNotice: React.FC<{ accountStatus: CloudAccountStatus }> = ({ accountStatus }) => {
    const { t } = useTranslate();
    const queryClient = useQueryClient();
    const syncMutation = useCloudSync();

    const blocked = accountStatus === CloudAccountStatus.BLOCKED;

    const check = async () => {
        await syncMutation.mutateAsync();
        // the sync reads the status in the background: refresh a bit later too
        await queryClient.invalidateQueries();
        setTimeout(() => void queryClient.invalidateQueries(), 2000);
    };

    return <Paper withBorder p='lg' radius='md'>
        <Stack gap='md'>
            <Group gap='sm' wrap='nowrap'>
                <ThemeIcon variant='light' color={blocked ? 'red' : 'yellow'} radius='xl' size='lg'>
                    {blocked ? <BanIcon /> : <ClockIcon />}
                </ThemeIcon>
                <Text fw={700} size='lg'>{t(blocked ? 'cloud.approval.blocked.title' : 'cloud.approval.pending.title')}</Text>
                <Badge variant='light' color={blocked ? 'red' : 'yellow'}>
                    {t(blocked ? 'cloud.approval.blocked.badge' : 'cloud.approval.pending.badge')}
                </Badge>
            </Group>
            <Text size='sm'>{t(blocked ? 'cloud.approval.blocked.description' : 'cloud.approval.pending.description')}</Text>
            <Text size='sm' c='dimmed'>{t('cloud.approval.local')}</Text>
            {!blocked && <Group justify='flex-end'>
                <UIButton
                    name='cloud-approval-check'
                    controlLabel={t('cloud.approval.check')}
                    onClick={check}
                    loading={syncMutation.isPending}
                    leftSection={<RefreshCwIcon />}
                    variant='filled'
                >
                    {t('cloud.approval.check')}
                </UIButton>
            </Group>}
        </Stack>
    </Paper>;
};
