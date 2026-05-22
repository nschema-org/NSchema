DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'citext') THEN
            CREATE EXTENSION citext;
        END IF;
    END
$$;
