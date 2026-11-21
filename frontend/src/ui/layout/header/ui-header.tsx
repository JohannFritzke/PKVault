import { Badge, Box, Group, Stack, Title, useMatches, type MantineSpacing } from '@mantine/core';
import { clsx } from 'clsx';
import React from 'react';
import { WithControlsIcons } from '../../interaction/controls/icons/with-controls-icons';
import { FocusScope } from '../../interaction/focus/scope/focus-scope';
import { CurrentPanelProvider } from '../../storage/storage-content/context/ui-current-panel-provider';
import { PanelProvider } from '../../storage/storage-content/context/ui-panel-context';
import { usePanelControls } from '../hooks/use-panel-controls';
import { UIToggleColorScheme } from './sub-header/ui-toggle-color-scheme';
import classes from './ui-header.module.css';

export const UIHeader: React.FC<{
    left: React.ReactNode;
    right: React.ReactNode;
    sub?: React.ReactNode;
    demoMode?: boolean;
}> = ({ left, right, sub, demoMode }) => {

    const { panelProps, nodeId, childScopeId, controlIcons } = usePanelControls('header');

    const gap = useMatches<MantineSpacing>({
        base: 'xs',
        sm: 'sm'
    });

    return (
        <WithControlsIcons placement='in' icons={controlIcons('open')}
            {...panelProps}
            className={clsx(classes.uiHeader, panelProps.className)}
        >
            <CurrentPanelProvider value={'header'}>
                <PanelProvider value='header'>
                    <FocusScope id={childScopeId} parentNodeId={nodeId}>
                        <Stack gap={0} className={classes.main}>
                            <Group className={classes.firstLine} gap={gap} wrap='nowrap'>
                                <Group className={classes.brand} gap='sm' wrap='nowrap'>
                                    <img
                                        src="/logo.svg"
                                        alt=""
                                        className={classes.logo}
                                    />
                                    <Title order={4} lh={1} className={classes.logoTitle}>
                                        PKVault
                                    </Title>
                                    {demoMode && <Badge size='sm' variant='light'>DEMO</Badge>}
                                </Group>

                                <nav className={classes.nav}>
                                    {left}
                                </nav>

                                {gap !== 'xs' && <Box ml='auto' />}

                                {right}
                            </Group>

                            <Group
                                className={classes.secondLine}
                                miw={0}
                                align='flex-start'
                                wrap='nowrap'
                                gap='sm'
                            >
                                {sub}

                                <UIToggleColorScheme
                                    ml='auto'
                                />
                            </Group>
                        </Stack>
                    </FocusScope>
                </PanelProvider>
            </CurrentPanelProvider>
        </WithControlsIcons>
    );
};
