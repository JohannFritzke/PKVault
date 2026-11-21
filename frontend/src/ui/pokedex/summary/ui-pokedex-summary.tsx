import { Group, Paper, Progress, SimpleGrid } from '@mantine/core';
import type React from 'react';
import { UIPokedexIcons } from '../icons/ui-pokedex-icons';
import classes from './ui-pokedex-summary.module.css';

type UIPokedexSummaryProps = {
    seenCount: number;
    caughtCount: number;
    ownedCount: number;
    shinyCount: number;
    totalCount: number;
};

/**
 * Summary tiles on top of the pokedex grid (counts follow current filters).
 */
export const UIPokedexSummary: React.FC<UIPokedexSummaryProps> = ({
    seenCount, caughtCount, ownedCount, shinyCount, totalCount,
}) => {
    const renderTile = (icon: React.ReactNode, count: number) => <Paper withBorder radius='lg' px='lg' py='md' shadow='none'>
        <Group gap='xs' wrap='nowrap'>
            {icon}
            <span className={classes.value}>{count}</span>
            <span className={classes.total}>/ {totalCount}</span>
        </Group>

        <Progress
            value={totalCount > 0 ? count / totalCount * 100 : 0}
            size={4}
            radius='xl'
            mt='sm'
            transitionDuration={0}
        />
    </Paper>;

    return <SimpleGrid cols={{ base: 2, md: 4 }} spacing='md' verticalSpacing='md'>
        {renderTile(<UIPokedexIcons.Seen size='md' />, seenCount)}
        {renderTile(<UIPokedexIcons.Caught size='md' />, caughtCount)}
        {renderTile(<UIPokedexIcons.Owned size='md' />, ownedCount)}
        {renderTile(<UIPokedexIcons.Shiny size='md' />, shinyCount)}
    </SimpleGrid>;
};
