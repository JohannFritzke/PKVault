import { Alert, Anchor, Badge, Group, Loader, Text } from '@mantine/core';
import { CloudUploadIcon, LogInIcon, LogOutIcon, RefreshCwIcon, ServerIcon, UserRoundIcon } from 'lucide-react';
import React from 'react';
import { useCloudLogin, useCloudLogout, useCloudStatus, useCloudSync } from '../../data/sdk/cloud/cloud.gen';
import { BackupProviderPushState, CloudLoginError, CloudLoginState, CloudSyncError, CloudSyncState, type CloudStatusDTO } from '../../data/sdk/model';
import { useTranslate } from '../../translate/i18n';
import { UIButton } from '../../ui/form/button/ui-button';
import { UISettingsRow } from '../../ui/settings/section/ui-settings-row';
import { UISettingsSection } from '../../ui/settings/section/ui-settings-section';
import classes from '../../ui/settings/ui-settings.module.css';
import { renderDate, renderTime } from '../../util/render-date-time';

const providers = [ 'google', 'github' ] as const;

const providerLabel = (provider?: string) => provider === 'github' ? 'GitHub' : provider === 'google' ? 'Google' : provider ?? '';

const isBusy = (status?: CloudStatusDTO) => status?.loginState === CloudLoginState.WAITING_BROWSER
    || status?.backupState === BackupProviderPushState.PENDING
    || status?.syncState === CloudSyncState.CHECKING
    || status?.syncState === CloudSyncState.PULLING;

/**
 * Cloud account: login with Google/GitHub on the PokéHome server, and the state of backup uploads.
 */
export const SettingsCloudAccount: React.FC = () => {
    const { t } = useTranslate();

    const statusQuery = useCloudStatus({
        query: {
            refetchInterval: query => isBusy(query.state.data?.data) ? 1500 : 10_000,
        },
    });
    const status = statusQuery.data?.data;

    const loginMutation = useCloudLogin();
    const logoutMutation = useCloudLogout();
    const syncMutation = useCloudSync();

    const [ authUrl, setAuthUrl ] = React.useState<string>();

    const login = async (provider: string) => {
        const result = await loginMutation.mutateAsync({ data: { provider } });
        setAuthUrl(result.data.authUrl);
        if (!result.data.browserOpened)
            window.open(result.data.authUrl, '_blank', 'noopener');
        await statusQuery.refetch();
    };

    const syncNow = async () => {
        await syncMutation.mutateAsync();
        await statusQuery.refetch();
    };

    const logout = async () => {
        await logoutMutation.mutateAsync();
        setAuthUrl(undefined);
        await statusQuery.refetch();
    };

    if (!status) {
        return <UISettingsSection padded>
            <Loader size='sm' />
        </UISettingsSection>;
    }

    const waiting = status.loginState === CloudLoginState.WAITING_BROWSER;

    const loginErrorText = status.loginState === CloudLoginState.ERROR && status.loginError !== undefined
        ? {
            [ CloudLoginError.TIMEOUT ]: t('cloud.login.error.timeout'),
            [ CloudLoginError.DENIED ]: t('cloud.login.error.denied'),
            [ CloudLoginError.SERVER ]: t('cloud.login.error.server'),
            [ CloudLoginError.EXPIRED ]: t('cloud.login.error.expired'),
        }[ status.loginError ]
        : undefined;

    const renderBackup = () => {
        if (status.backupState === BackupProviderPushState.PENDING)
            return <Group gap='xs'><Loader size='xs' />{t('cloud.backup.pending')}</Group>;

        if (status.backupState === BackupProviderPushState.SUCCESS && status.lastBackupAt) {
            const date = new Date(status.lastBackupAt);
            return t('cloud.backup.success', { date: renderDate(date), time: renderTime(date) });
        }

        if (status.backupState === BackupProviderPushState.FAILED)
            return t('cloud.backup.failed', { error: status.backupError ?? '' });

        return t('cloud.backup.none');
    };

    const renderSync = () => {
        switch (status.syncState) {
            case CloudSyncState.CHECKING:
                return <Group gap='xs'><Loader size='xs' />{t('cloud.sync.checking')}</Group>;
            case CloudSyncState.PULLING:
                return <Group gap='xs'><Loader size='xs' />{t('cloud.sync.pulling')}</Group>;
            case CloudSyncState.WAITING_LOCAL_CHANGES:
                return t('cloud.sync.waiting-local');
            case CloudSyncState.ERROR:
                return <>
                    {{
                        [ CloudSyncError.SERVER ]: t('cloud.sync.error.server'),
                        [ CloudSyncError.INVALID_BACKUP ]: t('cloud.sync.error.invalid'),
                        [ CloudSyncError.UNSUPPORTED_PATHS ]: t('cloud.sync.error.paths'),
                    }[ status.syncError ?? CloudSyncError.SERVER ]}
                    {status.syncErrorDetail && <Text size='xs' c='dimmed' className={classes.mono}>{status.syncErrorDetail}</Text>}
                </>;
        }

        const lines: string[] = [];
        if (status.lastPulledDevice && status.lastPulledBackupAt) {
            const date = new Date(status.lastPulledBackupAt);
            lines.push(t('cloud.sync.pulled', { device: status.lastPulledDevice, date: renderDate(date), time: renderTime(date) }));
            if (status.lastPulledSavesSkipped > 0)
                lines.push(t('cloud.sync.saves-skipped', { count: status.lastPulledSavesSkipped }));
        }
        if (status.lastSyncAt) {
            const date = new Date(status.lastSyncAt);
            lines.push(t('cloud.sync.checked', { date: renderDate(date), time: renderTime(date) }));
        }
        return lines.length > 0
            ? lines.map(line => <div key={line}>{line}</div>)
            : t('cloud.sync.never');
    };

    return <>
        <UISettingsSection
            title={t('cloud.account.title')}
            description={t('cloud.account.description')}
            actions={status.loggedIn && <UIButton
                name='cloud-logout'
                controlLabel={t('cloud.logout')}
                onClick={logout}
                loading={logoutMutation.isPending}
                leftSection={<LogOutIcon />}
            >
                {t('cloud.logout')}
            </UIButton>}
        >
            <UISettingsRow icon={<UserRoundIcon />}
                label={t('cloud.account.label')}
                description={status.loggedIn
                    ? [ status.userName, status.userEmail ].filter(Boolean).join(' · ') || '-'
                    : t('cloud.account.logged-out')}
            >
                {status.loggedIn && status.provider && <Badge variant='default'>
                    {t('cloud.account.via', { provider: providerLabel(status.provider) })}
                </Badge>}
            </UISettingsRow>

            {status.loggedIn && <UISettingsRow icon={<CloudUploadIcon />}
                label={t('cloud.backup.label')}
                description={renderBackup()}
            />}

            {status.loggedIn && <UISettingsRow icon={<RefreshCwIcon />}
                label={t('cloud.sync.label')}
                description={renderSync()}
            >
                <UIButton
                    name='cloud-sync'
                    controlLabel={t('cloud.sync.now')}
                    onClick={syncNow}
                    loading={syncMutation.isPending}
                    disabled={status.syncState === CloudSyncState.CHECKING || status.syncState === CloudSyncState.PULLING}
                    leftSection={<RefreshCwIcon />}
                >
                    {t('cloud.sync.now')}
                </UIButton>
            </UISettingsRow>}

            <UISettingsRow icon={<ServerIcon />}
                label={t('cloud.server.label')}
                description={<span className={classes.mono}>{status.serverUrl}</span>}
            />
        </UISettingsSection>

        {!status.loggedIn && <UISettingsSection
            title={t('cloud.login.title')}
            description={t('cloud.login.same-provider')}
            padded
        >
            <Group gap='sm'>
                {providers.map(provider => <UIButton
                    key={provider}
                    name={`cloud-login-${provider}`}
                    controlLabel={t('cloud.login.with', { provider: providerLabel(provider) })}
                    onClick={() => login(provider)}
                    loading={loginMutation.isPending && loginMutation.variables?.data.provider === provider}
                    disabled={loginMutation.isPending}
                    variant={waiting && status.loginProvider === provider ? 'filled' : 'default'}
                    leftSection={<LogInIcon />}
                >
                    {t('cloud.login.with', { provider: providerLabel(provider) })}
                </UIButton>)}
            </Group>

            {waiting && <Alert variant='outline' color='gray' mt='md' icon={<Loader size='xs' />}>
                {t('cloud.login.waiting', { provider: providerLabel(status.loginProvider) })}
                {authUrl && <>
                    {' '}
                    <Anchor href={authUrl} target='_blank' rel='noopener'>{t('cloud.login.open-link')}</Anchor>
                </>}
            </Alert>}

            {loginErrorText && <Alert variant='outline' color='yellow' mt='md'>
                {loginErrorText}
                {status.loginErrorDetail && <Text size='xs' c='dimmed' mt={4} className={classes.mono}>{status.loginErrorDetail}</Text>}
            </Alert>}
        </UISettingsSection>}
    </>;
};
